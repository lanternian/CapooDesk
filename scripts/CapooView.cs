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

	private CapooInfo capooInfo;
	private CapooConfig config;

	private SpineSprite face, faceDecoration, hat, clothes, back, rightHandItem;
	private SpineSprite accTop, accBody, accBot;
	private SpineSprite mood;
	private SpineSprite efxBot, efxTop, efxAppear;
	private SpineSprite talk, food, poo;

	private string currentAction;
	private float petScale = 0.5f;

	private SpineAnimationState bodyAnimationState, faceAnimationState, moodAnimationState;
	private SpineAnimationState efxBotAnimationState, efxTopAnimationState, efxAppearAnimationState;
	private SpineAnimationState talkAnimationState, foodAnimationState, pooAnimationState;

	/// <summary>Components hidden while the summon/recycle effect covers the character.</summary>
	private readonly List<CanvasItem> characterParts = new List<CanvasItem>();

	/// <summary>Hats, backs, props... that should also be clickable.</summary>
	private readonly List<SpineSprite> accessories = new List<SpineSprite>();

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
		SkeletonDataRes = GD.Load<SpineSkeletonDataResource>($"res://assets/spine/body/{capooInfo.body}.res");
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
		var sprite = AttachComponent(nodeName, boneName, $"res://assets/spine/{folder}/{resName}.res", anim, behindBody);
		if (sprite != null)
		{
			characterParts.Add(sprite);
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
			efxAppearAnimationState.SetAnimation(capooAction.appear, capooAction.appear_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.food))
			foodAnimationState.SetAnimation(capooAction.food, capooAction.food_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.poo))
			pooAnimationState.SetAnimation(capooAction.poo, capooAction.poo_loop, 0);

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
			efxAppearAnimationState.AddAnimation(capooAction.appear, delay, capooAction.appear_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.talk))
			talkAnimationState.AddAnimation(capooAction.talk, delay, capooAction.talk_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.food))
			foodAnimationState.AddAnimation(capooAction.food, delay, capooAction.food_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.poo))
			pooAnimationState.AddAnimation(capooAction.poo, delay, capooAction.poo_loop, 0);
	}

	private static void PlayOrHide(SpineAnimationState state, string anim, bool loop)
	{
		if (string.IsNullOrEmpty(anim))
			state.SetAnimation("hide_all", true, 0);
		else
			state.SetAnimation(anim, loop, 0);
	}

	/// <summary>Plays a raw animation on one layer without touching the others.</summary>
	public void SetLayer(Layer layer, string anim, bool loop = true)
	{
		var state = GetLayerState(layer);
		state?.SetAnimation(string.IsNullOrEmpty(anim) ? "hide_all" : anim, loop, 0);
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
		moodAnimationState.ClearTrack(0);
		efxTopAnimationState.ClearTrack(0);
		efxBotAnimationState.ClearTrack(0);
		efxAppearAnimationState.ClearTrack(0);
		talkAnimationState.ClearTrack(0);
		foodAnimationState.ClearTrack(0);
		pooAnimationState.ClearTrack(0);
	}

	public void SetEmptyAnimation()
	{
		bodyAnimationState.SetAnimation("hide_all");
		faceAnimationState.SetAnimation("hide_all");
		moodAnimationState.SetAnimation("hide_all");
		efxTopAnimationState.SetAnimation("hide_all");
		efxBotAnimationState.SetAnimation("hide_all");
		efxAppearAnimationState.SetAnimation("hide_all");
		talkAnimationState.SetAnimation("hide_all");
		foodAnimationState.SetAnimation("hide_all");
		pooAnimationState.SetAnimation("hide_all");
	}

	public void ClearAnimation()
	{
		ClearTrack();
		SetEmptyAnimation();
		currentAction = null;
	}

	// ---- Appearance ----

	public void SetPetScale(float scale)
	{
		petScale = scale;
		Scale = new Vector2(FacingRight ? -scale : scale, scale);
	}

	public void SetFacing(bool right)
	{
		Scale = new Vector2(right ? -petScale : petScale, petScale);
		// Keep the bubble icons ("?", "!") readable when mirrored.
		if (talk != null) talk.Scale = new Vector2(right ? -2.0f : 2.0f, 2.0f);
	}

	/// <summary>Hide/show the character itself while the summon or recycle effect plays.</summary>
	public void SetCharacterVisible(bool visible)
	{
		SelfModulate = visible ? Colors.White : Colors.Transparent;
		foreach (var part in characterParts) part.Visible = visible;
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
			if (part.IsVisibleInTree() && GetGlobalBounds(part).HasPoint(world)) return true;
		}
		return false;
	}

	private static Rect2 GetGlobalBounds(SpineSprite sprite)
	{
		if (sprite == null) return new Rect2();
		Rect2 local = sprite.GetSkeleton().GetBounds();
		if (local.Size == Vector2.Zero) return new Rect2();
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

		mood = AttachComponent("mood", "body_mood", $"res://assets/spine/mood/{capooInfo.mood}.res", capooInfo.mood_Anim);
		moodAnimationState = mood.GetAnimationState();

		efxBot = AttachComponent("efx_bot", "root", $"res://assets/spine/efx_bot/{capooInfo.efx_bot}.res", capooInfo.efx_bot_Anim, behindBody: true);
		efxBotAnimationState = efxBot.GetAnimationState();

		efxTop = AttachComponent("efx_top", "root", $"res://assets/spine/efx_top/{capooInfo.efx_top}.res", capooInfo.efx_top_Anim);
		efxTopAnimationState = efxTop.GetAnimationState();

		efxAppear = AttachComponent("efx_appears", "root", $"res://assets/spine/efx_appear/{capooInfo.efx_appears}.res", capooInfo.efx_appears_Anim);
		efxAppearAnimationState = efxAppear.GetAnimationState();

		talk = AttachComponent("talk", "talk", $"res://assets/spine/char_talk/{capooInfo.talk}.res", capooInfo.talk_Anim);
		talkAnimationState = talk.GetAnimationState();

		food = AttachComponent("food", "root", $"res://assets/spine/food/{capooInfo.food}.res", capooInfo.food_Anim);
		foodAnimationState = food.GetAnimationState();

		poo = AttachComponent("poo", "root", $"res://assets/spine/poo/{capooInfo.poo}.res", capooInfo.poo_Anim);
		pooAnimationState = poo.GetAnimationState();
		SetPooSkin(poo, "poo_01");
		#endregion

		characterParts.Add(mood);
		ClearAnimation();
		SetFacing(FacingRight);
	}
}
