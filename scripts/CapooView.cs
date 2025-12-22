using Godot;
using System;
using System.Collections.Generic;

public partial class CapooView : SpineSprite
{
    public int id = 0;

	private CapooInfo capooInfo;
	private List<CapooAction> capooActionList;

	private SpineSprite face, faceDecoration, hat, clothes, back, rightHandItem;
	private SpineSprite accTop, accBody, accBot;
    private SpineSprite mood;
	private SpineSprite efxBot, efxTop, efxAppear;
    private SpineSprite talk, food, poo;

	private string currentAction;

	private SpineAnimationState bodyAnimationState, faceAnimationState, moodAnimationState;
	private SpineAnimationState efxBotAnimationState, efxTopAnimationState, efxAppearAnimationState;
	private SpineAnimationState talkAnimationState, foodAnimationState, pooAnimationState;
	
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

    private SpineSprite AttachComponent(string nodeName, string boneName, string resPath, string anim = "")
    {
        // SpineBoneNode
        SpineBoneNode boneNode = new SpineBoneNode();
        boneNode.Name = nodeName + "_bone";
        boneNode.Set("bone_name", boneName);
        AddChild(boneNode);

        // SpineSprite
        SpineSprite sprite = new SpineSprite();
        sprite.Name = nodeName;
        sprite.SkeletonDataRes = GD.Load<SpineSkeletonDataResource>(resPath);
        sprite.Scale = new Vector2(2.0f, 2.0f);
        boneNode.AddChild(sprite);

        // Apearance Animation
        if (!string.IsNullOrEmpty(anim))
        {
            sprite.GetAnimationState().SetAnimation(anim, true, 0);
        }

        return sprite;
    }
	
	private static void SetPooSkin(SpineSprite sprite, string skinName)
    {
        var skeleton = sprite.GetSkeleton();
		skeleton.SetSkinByName(skinName);
		skeleton.SetToSetupPose();
    }
	
	public void SetAnimation(string actionName, bool faceAniChange = true)
	{
		if (currentAction == actionName) return;

		var capooAction = capooActionList.Find(action => action.action_name == actionName);
		if (!string.IsNullOrEmpty(capooAction.body))
			bodyAnimationState.SetAnimation(capooAction.body, capooAction.body_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.face) && faceAniChange)
			if (faceAniChange) faceAnimationState.SetAnimation(capooAction.face, capooAction.face_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.mood))
			moodAnimationState.SetAnimation(capooAction.mood, capooAction.mood_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.effectTop))
			efxTopAnimationState.SetAnimation(capooAction.effectTop, capooAction.effectTop_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.effectBot))
			efxBotAnimationState.SetAnimation(capooAction.effectBot, capooAction.effectBot_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.appear))
			efxAppearAnimationState.SetAnimation(capooAction.appear, capooAction.appear_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.talk))
			talkAnimationState.SetAnimation(capooAction.talk, capooAction.talk_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.food))
			foodAnimationState.SetAnimation(capooAction.food, capooAction.food_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.poo))
			pooAnimationState.SetAnimation(capooAction.poo, capooAction.poo_loop, 0);

		currentAction = actionName;
	}
	
	public void AddAnimation(string actionName, float delay = 0, bool faceAniChange = true)
	{
		var capooAction = capooActionList.Find(action => action.action_name == actionName);
		if (!string.IsNullOrEmpty(capooAction.body))
			bodyAnimationState.AddAnimation(capooAction.body, delay, capooAction.body_loop, 0);
		if (!string.IsNullOrEmpty(capooAction.face) && faceAniChange)
			if (faceAniChange) faceAnimationState.AddAnimation(capooAction.face, delay, capooAction.face_loop, 0);
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
	}

	public override void _Ready()
	{
		#region Capoo Component Initialization
		var config = GetNode<CapooConfig>("/root/CapooConfig");
		capooInfo = config.infos[id];
		capooActionList = config.actions;
		
        InitBody();
		bodyAnimationState = GetAnimationState();
		if (capooInfo.handIndex) 
			bodyAnimationState.SetAnimation("hand_prop_r_01", true, 1); // hand index track

        face = AttachComponent("face", "body_face_root", $"res://assets/spine/face/{capooInfo.face}.res", capooInfo.face_Anim);
		faceAnimationState = face.GetAnimationState();
        
		if (!string.IsNullOrEmpty(capooInfo.face_decoration))
        	faceDecoration = AttachComponent("face_decoration", "mask", $"res://assets/spine/face_decoration/{capooInfo.face_decoration}.res", capooInfo.face_decoration_Anim);
        
		if (!string.IsNullOrEmpty(capooInfo.hat))
            hat = AttachComponent("hat", "hat", $"res://assets/spine/hat/{capooInfo.hat}.res", capooInfo.hat_Anim);
			if (capooInfo.hat == "capoo_hat_009") // exception for capoo_001
			{
				var hatAnimationState = hat.GetAnimationState();
				hatAnimationState.SetAnimation("hide_all", false, 0);
			}
        
		if (!string.IsNullOrEmpty(capooInfo.clothes))
            clothes = AttachComponent("clothes", "clothes", $"res://assets/spine/clothes/{capooInfo.clothes}.res", capooInfo.clothes_Anim);
        
		if (!string.IsNullOrEmpty(capooInfo.back))
            back = AttachComponent("back", "back", $"res://assets/spine/back/{capooInfo.back}.res", capooInfo.back_Anim);
			if (!string.IsNullOrEmpty(capooInfo.back_hierarchy)) back.ZIndex = int.Parse(capooInfo.back_hierarchy) -1;
        
		if (!string.IsNullOrEmpty(capooInfo.right_hand_item))
        	rightHandItem = AttachComponent("right_hand_item", "prop_r", $"res://assets/spine/prop/{capooInfo.right_hand_item}.res", capooInfo.right_hand_item_Anim);

		if (!string.IsNullOrEmpty(capooInfo.capoo_acc_top))
        	accTop = AttachComponent("capoo_acc_top", "root", $"res://assets/spine/acc/{capooInfo.capoo_acc_top}.res", capooInfo.capoo_acc_top_Anim);
		
		if (!string.IsNullOrEmpty(capooInfo.capoo_acc_body))
			accBody = AttachComponent("capoo_acc_body", "root", $"res://assets/spine/acc/{capooInfo.capoo_acc_body}.res", capooInfo.capoo_acc_body_Anim);
		
		if (!string.IsNullOrEmpty(capooInfo.capoo_acc_bot))
			accBot = AttachComponent("capoo_acc_bot", "root", $"res://assets/spine/acc/{capooInfo.capoo_acc_bot}.res", capooInfo.capoo_acc_bot_Anim);
		
		mood = AttachComponent("mood", "body_mood", $"res://assets/spine/mood/{capooInfo.mood}.res", capooInfo.mood_Anim);
		moodAnimationState = mood.GetAnimationState();

		efxBot = AttachComponent("efx_bot", "root", $"res://assets/spine/efx_bot/{capooInfo.efx_bot}.res", capooInfo.efx_bot_Anim);
		efxBotAnimationState = efxBot.GetAnimationState();
		efxBot.ZIndex = -2;  // effect in buttom

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

		// Appear Animation
		AddAnimation("summonAppear");
		AddAnimation("idle_idle1", 1, false);
		// talkAnimationState.SetAnimation("talk_hungry", true, 0);
		

	}

	public override void _Process(double delta)
	{
		// float Speed = 200.0f;
		// Vector2 inputDir = Input.GetVector("left", "right", "up", "down");
		
		// if (inputDir != Vector2.Zero)
		// {
		// 	Position += inputDir * Speed * (float)delta;

		// 	if (inputDir.X != 0)
		// 	{
		// 		Scale = new Vector2(-Mathf.Sign(inputDir.X) * Mathf.Abs(Scale.X), Scale.Y);
		// 	}

		// 	SetAnimation("walk2", false); 
		// }
		// else if (Input.IsActionJustReleased("left") || Input.IsActionJustReleased("right") || Input.IsActionJustReleased("up") || Input.IsActionJustReleased("down"))
		// {
		// 	SetAnimation("idle_idle1", false);
		// }
	}
}
