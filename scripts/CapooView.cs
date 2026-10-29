using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Spine rendering of one Capoo: the body skeleton plus up to 16 component skeletons
/// (face, hat, back, props, effects, talk bubble, food, poo) pinned to body bones.
/// Actions from Capoo_Action.json drive the animated layers together.
/// </summary>
public partial class CapooView : SpineSprite
{
	public enum Layer { Body, Face, Mood, EffectTop, EffectBot, Talk, Appear, Food, Poo }

	public int id = 0;

	public CapooInfo Info => capooInfo;
	public string CurrentAction => currentAction;

	/// <summary>The art faces left; mirrored when facing right.</summary>
	public bool FacingRight => Scale.X < 0;

	/// <summary>The summon/recycle smoke. It is not a child of the view: the character can be
	/// hidden while the smoke keeps playing, so the controller parents it next to the view.</summary>
	public SpineSprite AppearSprite => efxAppear;

	/// <summary>Scale of the body skeleton's root bone (0.5 for most characters). The smoke
	/// hangs off that bone, so it needs the same factor to sit where it used to.</summary>
	public float RootBoneScale { get; private set; } = 0.5f;

	private CapooInfo capooInfo;
	private CapooConfig config;

	private SpineSprite face, faceDecoration, hat, clothes, back, rightHandItem;
	private SpineSprite accTop, accBody, accBot;
	private SpineSprite mood;
	private SpineSprite efxBot, efxTop, efxAppear;
	private SpineSprite talk, food, poo;

	private string currentAction;
	private float petScale = 0.5f;
	private double appearTimer;

	private SpineAnimationState bodyAnimationState, faceAnimationState, moodAnimationState;
	private SpineAnimationState efxBotAnimationState, efxTopAnimationState, efxAppearAnimationState;
	private SpineAnimationState talkAnimationState, foodAnimationState, pooAnimationState;

	/// <summary>Components hidden while the summon/recycle effect covers the character.</summary>
	private readonly List<SpineSprite> characterParts = new List<SpineSprite>();

	/// <summary>Hats, backs, props... that should also be clickable.</summary>
	private readonly List<SpineSprite> accessories = new List<SpineSprite>();

	/// <summary>The name of the skeleton behind the body and behind every part, used to
	/// look up the clickable shapes of the artwork.</summary>
	private string bodySkeleton;
	private readonly Dictionary<SpineSprite, string> partSkeletons = new Dictionary<SpineSprite, string>();
	private readonly Dictionary<SpineSprite, SlotCache> slotCaches = new Dictionary<SpineSprite, SlotCache>();
	private readonly List<(SpineSprite sprite, string skeleton)> decorativeParts = new List<(SpineSprite, string)>();

	public CapooView()
	{
	}

	public CapooView(int id)
	{
		this.id = id;
	}

	private void InitBody()
	{
		Name = "body";
		string path = $"res://assets/spine/body/{capooInfo.body}.res";
		SkeletonDataRes = GD.Load<SpineSkeletonDataResource>(path);
		bodySkeleton = SkeletonName(path);
	}

	/// <summary>The name of the skeleton file behind a Spine resource.</summary>
	private static string SkeletonName(string resPath)
	{
		int slash = resPath.LastIndexOf('/');
		string file = slash >= 0 ? resPath.Substring(slash + 1) : resPath;
		int dot = file.LastIndexOf('.');
		return dot > 0 ? file.Substring(0, dot) : file;
	}

	private SpineSprite AttachComponent(string nodeName, string boneName, string resPath, string anim = "", bool behindBody = false)
	{
		var data = GD.Load<SpineSkeletonDataResource>(resPath);
		if (data == null)
		{
			GD.PrintErr($"Missing Spine resource {resPath} for {capooInfo.id}");
			return null;
		}

		// SpineBoneNode
		SpineBoneNode boneNode = new SpineBoneNode();
		boneNode.Name = nodeName + "_bone";
		boneNode.Set("bone_name", boneName);
		boneNode.ShowBehindParent = behindBody;
		AddChild(boneNode);

		// SpineSprite
		SpineSprite sprite = new SpineSprite();
		sprite.Name = nodeName;
		sprite.SkeletonDataRes = data;
		sprite.Scale = new Vector2(2.0f, 2.0f);
		boneNode.AddChild(sprite);

		// Apearance Animation
		if (!string.IsNullOrEmpty(anim))
		{
			sprite.GetAnimationState().SetAnimation(anim, true, 0);
		}

		return sprite;
	}

	private SpineSprite AttachPart(string nodeName, string boneName, string folder, string resName, string anim, bool behindBody = false)
	{
		if (string.IsNullOrEmpty(resName)) return null;
		string path = $"res://assets/spine/{folder}/{resName}.res";
		var sprite = AttachComponent(nodeName, boneName, path, anim, behindBody);
		if (sprite != null)
		{
			characterParts.Add(sprite);
			partSkeletons[sprite] = SkeletonName(path);
			if (folder != "face") accessories.Add(sprite);
		}
		return sprite;
	}

	private static void SetPooSkin(SpineSprite sprite, string skinName)
	{
		var skeleton = sprite.GetSkeleton();
		skeleton.SetSkinByName(skinName);
		skeleton.SetToSetupPose();
	}

	// ---- Actions ----

	/// <summary>Plays an action on every layer it defines. Mood/effect/talk layers the action
	/// leaves empty are hidden, like the original; food, poo and appear keep playing.</summary>
	public bool SetAnimation(string actionName, bool faceAniChange = true, bool force = false)
	{
		if (!force && currentAction == actionName) return true;

		var capooAction = config.GetAction(actionName);
		if (capooAction == null) return false;

		if (!string.IsNullOrEmpty(capooAction.body))
			bodyAnimationState.SetAnimation(capooAction.body, capooAction.body_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.face) && faceAniChange)
			faceAnimationState.SetAnimation(capooAction.face, capooAction.face_loop, 0);
		PlayOrHide(moodAnimationState, capooAction.mood, capooAction.mood_loop);
		PlayOrHide(efxTopAnimationState, capooAction.effectTop, capooAction.effectTop_loop);
		PlayOrHide(efxBotAnimationState, capooAction.effectBot, capooAction.effectBot_loop);
		PlayOrHide(talkAnimationState, capooAction.talk, capooAction.talk_loop);
		if (!string.IsNullOrEmpty(capooAction.appear))
		{
			efxAppearAnimationState?.SetAnimation(capooAction.appear, capooAction.appear_loop, 0);
			MarkLayer(efxAppearAnimationState, true);
			appearTimer = GetAnimationDuration(efxAppear, capooAction.appear) + 0.1;
		}
		if (!string.IsNullOrEmpty(capooAction.food))
		{
			foodAnimationState.SetAnimation(capooAction.food, capooAction.food_loop, 0);
			MarkLayer(foodAnimationState, true);
		}
		if (!string.IsNullOrEmpty(capooAction.poo))
		{
			pooAnimationState.SetAnimation(capooAction.poo, capooAction.poo_loop, 0);
			MarkLayer(pooAnimationState, true);
		}

		currentAction = actionName;
		return true;
	}

	public void AddAnimation(string actionName, float delay = 0, bool faceAniChange = true)
	{
		var capooAction = config.GetAction(actionName);
		if (capooAction == null) return;

		if (!string.IsNullOrEmpty(capooAction.body))
			bodyAnimationState.AddAnimation(capooAction.body, delay, capooAction.body_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.face) && faceAniChange)
			faceAnimationState.AddAnimation(capooAction.face, delay, capooAction.face_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.mood))
			moodAnimationState.AddAnimation(capooAction.mood, delay, capooAction.mood_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.effectTop))
			efxTopAnimationState.AddAnimation(capooAction.effectTop, delay, capooAction.effectTop_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.effectBot))
			efxBotAnimationState.AddAnimation(capooAction.effectBot, delay, capooAction.effectBot_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.appear))
			efxAppearAnimationState?.AddAnimation(capooAction.appear, delay, capooAction.appear_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.talk))
			talkAnimationState.AddAnimation(capooAction.talk, delay, capooAction.talk_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.food))
			foodAnimationState.AddAnimation(capooAction.food, delay, capooAction.food_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.poo))
			pooAnimationState.AddAnimation(capooAction.poo, delay, capooAction.poo_loop, 0);
	}

	private void PlayOrHide(SpineAnimationState state, string anim, bool loop)
	{
		if (state == null) return;
		if (string.IsNullOrEmpty(anim))
			state.SetAnimation("hide_all", true, 0);
		else
			state.SetAnimation(anim, loop, 0);
		MarkLayer(state, !string.IsNullOrEmpty(anim));
	}

	/// <summary>Plays a raw animation on one layer without touching the others.</summary>
	public void SetLayer(Layer layer, string anim, bool loop = true)
	{
		var state = GetLayerState(layer);
		if (state == null) return;
		bool show = !string.IsNullOrEmpty(anim) && anim != "hide_all";
		state.SetAnimation(show ? anim : "hide_all", loop, 0);
		MarkLayer(state, show);
	}

	// ---- Sleeping layers ----
	// Overlay skeletons (bubble, food, poo, effects) spend most of their time on "hide_all".
	// Once hidden they stop processing entirely and wake up when given an animation again.

	private readonly Dictionary<SpineAnimationState, SpineSprite> overlaySprites = new Dictionary<SpineAnimationState, SpineSprite>();
	private readonly Dictionary<SpineSprite, int> hideCountdown = new Dictionary<SpineSprite, int>();

	private void RegisterOverlay(SpineSprite sprite, SpineAnimationState state)
	{
		if (sprite == null || state == null) return;
		overlaySprites[state] = sprite;
		MarkLayer(state, false);
	}

	private void MarkLayer(SpineAnimationState state, bool active)
	{
		if (state == null) return;
		if (!overlaySprites.TryGetValue(state, out var sprite) || sprite == null) return;
		if (active)
		{
			hideCountdown.Remove(sprite);
			sprite.ProcessMode = ProcessModeEnum.Inherit;
			sprite.Visible = true;
		}
		else if (sprite.ProcessMode != ProcessModeEnum.Disabled)
		{
			// Let "hide_all" apply for a couple of frames before freezing the sprite.
			hideCountdown[sprite] = 2;
		}
	}

	public override void _Process(double delta)
	{
		if (appearTimer > 0 && (appearTimer -= delta) <= 0)
			SetLayer(Layer.Appear, "hide_all");

		if (hideCountdown.Count == 0) return;
		foreach (var sprite in new List<SpineSprite>(hideCountdown.Keys))
		{
			int left = hideCountdown[sprite] - 1;
			if (left > 0)
			{
				hideCountdown[sprite] = left;
				continue;
			}
			hideCountdown.Remove(sprite);
			sprite.Visible = false;
			sprite.ProcessMode = ProcessModeEnum.Disabled;
		}
	}

	public void HideLayer(Layer layer) => SetLayer(layer, "hide_all");

	private SpineAnimationState GetLayerState(Layer layer) => layer switch
	{
		Layer.Body => bodyAnimationState,
		Layer.Face => faceAnimationState,
		Layer.Mood => moodAnimationState,
		Layer.EffectTop => efxTopAnimationState,
		Layer.EffectBot => efxBotAnimationState,
		Layer.Talk => talkAnimationState,
		Layer.Appear => efxAppearAnimationState,
		Layer.Food => foodAnimationState,
		Layer.Poo => pooAnimationState,
		_ => null
	};

	/// <summary>Length of the body animation used by an action (0 if unknown or a pose).</summary>
	public float GetActionDuration(string actionName)
	{
		var capooAction = config.GetAction(actionName);
		if (capooAction == null) return 0;
		if (!string.IsNullOrEmpty(capooAction.body)) return GetAnimationDuration(this, capooAction.body);
		if (!string.IsNullOrEmpty(capooAction.appear)) return GetAnimationDuration(efxAppear, capooAction.appear);
		return 0;
	}

	public float GetLayerAnimationDuration(Layer layer, string anim)
	{
		SpineSprite sprite = layer switch
		{
			Layer.Body => this,
			Layer.Face => face,
			Layer.Mood => mood,
			Layer.EffectTop => efxTop,
			Layer.EffectBot => efxBot,
			Layer.Talk => talk,
			Layer.Appear => efxAppear,
			Layer.Food => food,
			Layer.Poo => poo,
			_ => null
		};
		return GetAnimationDuration(sprite, anim);
	}

	private static float GetAnimationDuration(SpineSprite sprite, string anim)
	{
		var animation = sprite?.SkeletonDataRes?.FindAnimation(anim);
		return animation?.GetDuration() ?? 0;
	}

	public void ClearTrack()
	{
		bodyAnimationState.ClearTrack(0);
		faceAnimationState.ClearTrack(0);
		moodAnimationState?.ClearTrack(0);
		efxTopAnimationState?.ClearTrack(0);
		efxBotAnimationState?.ClearTrack(0);
		efxAppearAnimationState?.ClearTrack(0);
		talkAnimationState?.ClearTrack(0);
		foodAnimationState?.ClearTrack(0);
		pooAnimationState?.ClearTrack(0);
	}

	public void SetEmptyAnimation()
	{
		bodyAnimationState.SetAnimation("hide_all");
		faceAnimationState.SetAnimation("hide_all");
		moodAnimationState?.SetAnimation("hide_all");
		efxTopAnimationState?.SetAnimation("hide_all");
		efxBotAnimationState?.SetAnimation("hide_all");
		efxAppearAnimationState?.SetAnimation("hide_all");
		talkAnimationState?.SetAnimation("hide_all");
		foodAnimationState?.SetAnimation("hide_all");
		pooAnimationState?.SetAnimation("hide_all");
	}

	public void ClearAnimation()
	{
		ClearTrack();
		SetEmptyAnimation();
		foreach (var state in overlaySprites.Keys) MarkLayer(state, false);
		currentAction = null;
	}

	// ---- Appearance ----

	public void SetPetScale(float scale)
	{
		petScale = scale;
		Scale = new Vector2(FacingRight ? -scale : scale, scale);
		ApplyAppearScale();
	}

	public void SetFacing(bool right)
	{
		Scale = new Vector2(right ? -petScale : petScale, petScale);
		// Keep the bubble icons ("?", "!") readable when mirrored.
		if (talk != null) talk.Scale = new Vector2(right ? -2.0f : 2.0f, 2.0f);
		ApplyAppearScale();
	}

	/// <summary>The smoke hangs off the body's root bone; the bone node that used to carry it
	/// scaled it by the bone scale, the sprite itself by two.</summary>
	private void ApplyAppearScale()
	{
		if (efxAppear == null) return;
		float s = petScale * RootBoneScale * 2f;
		efxAppear.Scale = new Vector2(FacingRight ? -s : s, s);
	}

	/// <summary>Hide/show the character itself while the summon or recycle effect plays.
	/// The whole view goes away, so body, face and accessories always appear together.</summary>
	public void SetCharacterVisible(bool visible)
	{
		if (Visible == visible) return;
		Visible = visible;
		if (visible) RefreshPose();
	}

	/// <summary>Apply the current pose right away. A part that was just created (or hidden)
	/// would otherwise show up a frame late, because the Spine skeleton is only applied
	/// while the sprite is visible in the tree.</summary>
	public void RefreshPose()
	{
		if (!IsVisibleInTree()) return;
		UpdateSkeleton(0);
		foreach (var part in characterParts) part.UpdateSkeleton(0);
		foreach (var sprite in overlaySprites.Values)
			if (sprite.IsVisibleInTree()) sprite.UpdateSkeleton(0);
	}

	/// <summary>Global position of a body bone (e.g. "talk", "center", "hat").</summary>
	public Vector2 GetBoneGlobalPosition(string boneName)
	{
		return GetGlobalBoneTransform(boneName).Origin;
	}

	/// <summary>Global rect of the talk bubble's current pose (empty when hidden).</summary>
	public Rect2 GetTalkBubbleRect() => GetGlobalBounds(talk);

	/// <summary>True if the point is on one of the accessories (hat, back, prop...).</summary>
	public bool AccessoryHitTest(Vector2 world)
	{
		foreach (var part in accessories)
		{
			if (HitTestSprite(part, world, partSkeletons[part])) return true;
		}
		return false;
	}

	/// <summary>True if the point is on the character: the body, the face or an accessory.
	/// Every attachment is checked against its own artwork, so the empty space around a
	/// Capoo does not react to clicks.</summary>
	public bool HitTest(Vector2 world)
	{
		if (!IsVisibleInTree()) return false;
		if (HitTestSprite(this, world, bodySkeleton)) return true;
		foreach (var part in characterParts)
		{
			if (HitTestSprite(part, world, partSkeletons[part])) return true;
		}
		return false;
	}

	/// <summary>Hides the effects (mood, sparkles) so a test can look at the character
	/// alone. They are drawn above the Capoo, but they are not part of it.</summary>
	public void SetDecorationsVisible(bool visible)
	{
		foreach (var (sprite, _) in decorativeParts) sprite.Visible = visible;
	}

	/// <summary>Checks one skeleton against the click. The shapes live in the space of the
	/// skeleton, the same space the meshes are drawn in.</summary>
	private bool HitTestSprite(SpineSprite sprite, Vector2 world, string skeletonName)
	{
		if (sprite == null || !sprite.IsVisibleInTree()) return false;
		var shapes = HitShapes.For(skeletonName);
		if (shapes == null || shapes.Count == 0) return false;

		Vector2 local = sprite.GetGlobalTransform().AffineInverse() * world;
		string[] names = SlotNames(sprite, shapes.Count);
		for (int i = 0; i < shapes.Count; i++)
		{
			string name = names[i];
			if (name == null) continue;
			if (!shapes[i].TryGetValue(name, out var shape)) continue;
			if (shape.Contains(local)) return true;
		}
		return false;
	}

	/// <summary>The attachment of every slot. Reading it from Spine creates an object for
	/// every slot, so it is only done once per frame.</summary>
	private string[] SlotNames(SpineSprite sprite, int count)
	{
		if (!slotCaches.TryGetValue(sprite, out var cache))
		{
			cache = new SlotCache();
			slotCaches[sprite] = cache;
		}
		ulong frame = Engine.GetProcessFrames();
		if (cache.Frame == frame && cache.Names != null && cache.Names.Length == count)
		{
			return cache.Names;
		}
		var names = new string[count];
		var slots = sprite.GetSkeleton().GetSlots();
		for (int i = 0; i < count && i < slots.Count; i++)
		{
			var attachment = slots[i].As<SpineSlot>()?.GetAttachment();
			names[i] = attachment?.GetAttachmentName();
		}
		cache.Names = names;
		cache.Frame = frame;
		return names;
	}

	private class SlotCache
	{
		public ulong Frame;
		public string[] Names;
	}

	/// <summary>Remembers a decorative overlay (mood, effect) so it counts as part of
	/// the character when a click is checked.</summary>
	private void AddDecorative(SpineSprite sprite, string resPath)
	{
		if (sprite == null) return;
		decorativeParts.Add((sprite, SkeletonName(resPath)));
	}

	private static Rect2 GetGlobalBounds(SpineSprite sprite)
	{
		if (sprite == null) return new Rect2();
		Rect2 local = sprite.GetSkeleton().GetBounds();
		// A skeleton without any artwork has no bounds at all (Spine reports FLT_MAX).
		if (local.Size.X <= 0 || local.Size.Y <= 0) return new Rect2();
		if (!float.IsFinite(local.Position.X) || !float.IsFinite(local.Position.Y)) return new Rect2();
		if (!float.IsFinite(local.Size.X) || !float.IsFinite(local.Size.Y)) return new Rect2();
		var xf = sprite.GetGlobalTransform();
		Vector2 a = xf * local.Position;
		Vector2 b = xf * (local.Position + local.Size);
		return new Rect2(new Vector2(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y)), (b - a).Abs());
	}

	public override void _Ready()
	{
		#region Capoo Component Initialization
		config = GetNode<CapooConfig>("/root/CapooConfig");
		capooInfo = config.infos[id];

		InitBody();
		bodyAnimationState = GetAnimationState();
		var rootBone = GetSkeleton()?.GetRootBone();
		if (rootBone != null) RootBoneScale = Mathf.Abs(rootBone.GetWorldScaleX());
		if (capooInfo.handIndex)
			bodyAnimationState.SetAnimation("hand_prop_r_01", true, 1); // hand index track

		face = AttachPart("face", "body_face_root", "face", capooInfo.face, capooInfo.face_Anim);
		faceAnimationState = face.GetAnimationState();

		faceDecoration = AttachPart("face_decoration", "mask", "mask", capooInfo.face_decoration, capooInfo.face_decoration_Anim);

		hat = AttachPart("hat", "hat", "hat", capooInfo.hat, capooInfo.hat_Anim);
		if (hat != null && capooInfo.hat == "capoo_hat_009") // exception for capoo_001
			hat.GetAnimationState().SetAnimation("hide_all", false, 0);

		clothes = AttachPart("clothes", "clothes", "clothes", capooInfo.clothes, capooInfo.clothes_Anim);

		bool backBehind = capooInfo.back_hierarchy == "0";
		back = AttachPart("back", "back", "back", capooInfo.back, capooInfo.back_Anim, backBehind);

		rightHandItem = AttachPart("right_hand_item", "prop_r", "prop_r", capooInfo.right_hand_item, capooInfo.right_hand_item_Anim);

		accTop = AttachPart("capoo_acc_top", "root", "capoo_acc_top", capooInfo.capoo_acc_top, capooInfo.capoo_acc_top_Anim);
		accBody = AttachPart("capoo_acc_body", "root", "capoo_acc_body", capooInfo.capoo_acc_body, capooInfo.capoo_acc_body_Anim);
		accBot = AttachPart("capoo_acc_bot", "root", "capoo_acc_bot", capooInfo.capoo_acc_bot, capooInfo.capoo_acc_bot_Anim);

		string moodPath = $"res://assets/spine/mood/{capooInfo.mood}.res";
		mood = AttachComponent("mood", "body_mood", moodPath, capooInfo.mood_Anim);
		moodAnimationState = mood.GetAnimationState();
		AddDecorative(mood, moodPath);

		string efxBotPath = $"res://assets/spine/efx_bot/{capooInfo.efx_bot}.res";
		efxBot = AttachComponent("efx_bot", "root", efxBotPath, capooInfo.efx_bot_Anim, behindBody: true);
		efxBotAnimationState = efxBot.GetAnimationState();
		AddDecorative(efxBot, efxBotPath);

		string efxTopPath = $"res://assets/spine/efx_top/{capooInfo.efx_top}.res";
		efxTop = AttachComponent("efx_top", "root", efxTopPath, capooInfo.efx_top_Anim);
		efxTopAnimationState = efxTop.GetAnimationState();
		AddDecorative(efxTop, efxTopPath);

		// The smoke is not attached to the body: it has to keep playing while the character
		// itself is hidden, so the controller moves it next to the view instead.
		var appearData = GD.Load<SpineSkeletonDataResource>($"res://assets/spine/efx_appear/{capooInfo.efx_appears}.res");
		if (appearData != null)
		{
			efxAppear = new SpineSprite { Name = "efx_appears", SkeletonDataRes = appearData, Visible = false };
			AddChild(efxAppear);
			efxAppearAnimationState = efxAppear.GetAnimationState();
		}
		else
		{
			GD.PrintErr($"Missing Spine resource efx_appear/{capooInfo.efx_appears}.res for {capooInfo.id}");
		}

		talk = AttachComponent("talk", "talk", $"res://assets/spine/char_talk/{capooInfo.talk}.res", capooInfo.talk_Anim);
		talkAnimationState = talk.GetAnimationState();

		food = AttachComponent("food", "root", $"res://assets/spine/food/{capooInfo.food}.res", capooInfo.food_Anim);
		foodAnimationState = food.GetAnimationState();

		poo = AttachComponent("poo", "root", $"res://assets/spine/poo/{capooInfo.poo}.res", capooInfo.poo_Anim);
		pooAnimationState = poo.GetAnimationState();
		SetPooSkin(poo, "poo_01");
		#endregion

		RegisterOverlay(mood, moodAnimationState);
		RegisterOverlay(efxBot, efxBotAnimationState);
		RegisterOverlay(efxTop, efxTopAnimationState);
		if (efxAppear != null) RegisterOverlay(efxAppear, efxAppearAnimationState);
		RegisterOverlay(talk, talkAnimationState);
		RegisterOverlay(food, foodAnimationState);
		RegisterOverlay(poo, pooAnimationState);
		ClearAnimation();
		SetFacing(FacingRight);
	}
}
