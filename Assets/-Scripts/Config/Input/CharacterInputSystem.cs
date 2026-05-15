using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterInputSystem : MonoBehaviour
{
    private InputController _inputController;

    private bool IsDialogueBlockingGameplayInput => FirstPickupDialogueController.IsBlockingPauseMenu;

    //Key Setting
    public Vector2 playerMovement
    {
        get => _inputController.PlayerInput.Movement.ReadValue<Vector2>();
    }

    public Vector2 cameraLook
    {
        get => _inputController.PlayerInput.CameraLook.ReadValue<Vector2>();
    }

    public bool playerLAtk
    {
        get => !IsDialogueBlockingGameplayInput &&
               !FirstPickupDialogueController.IsBlockingAttackInput &&
               _inputController.PlayerInput.LAtk.triggered;
    }
    
    public bool playerRAtk
    {
        get => !IsDialogueBlockingGameplayInput &&
               _inputController.PlayerInput.RAtk.phase == InputActionPhase.Performed;
    }
    public bool playerDefen
    {
        get => !IsDialogueBlockingGameplayInput &&
               _inputController.PlayerInput.Defen.phase == InputActionPhase.Performed;
    }

    public bool playerRun
    {
        get => !IsDialogueBlockingGameplayInput &&
               _inputController.PlayerInput.Run.phase == InputActionPhase.Performed;
    }

    public bool playerRoll
    {
        get => !IsDialogueBlockingGameplayInput &&
               _inputController.PlayerInput.Roll.triggered;
    }

    public bool playerCrouch
    {
        get => !IsDialogueBlockingGameplayInput &&
               _inputController.PlayerInput.Crouch.triggered;
    }

    //内部函数
    private void Awake()
    {
        if (_inputController == null)
            _inputController = new InputController();
    }

    private void OnEnable()
    {
        _inputController.Enable();
    }

    private void OnDisable()
    {
        _inputController.Disable();
    }
}
