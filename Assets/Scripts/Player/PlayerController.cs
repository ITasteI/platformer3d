using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    public NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // Animator state, replicated so other clients see this player actually walking/jumping
    // instead of standing frozen in idle (only the owner ever runs HandleAnimation locally).
    private readonly NetworkVariable<float> netSpeed = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<bool> netGrounded = new NetworkVariable<bool>(
        true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<bool> netCrouching = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<float> netVerticalVelocity = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    [Header("Movement")]
    public float moveSpeed = 7f;
    public float rotationSpeed = 12f;
    // Ground/air acceleration so movement eases in and out instead of snapping on/off. High enough
    // to stay responsive (~0.13s to full speed), but smooths abrupt reversals and stops.
    public float moveAcceleration = 55f;
    public float moveDeceleration = 45f;

    [Header("Jumping")]
    public float jumpHeight = 1.8f;
    public float gravity = -25f;
    public int maxJumps = 2;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;

    [Header("Death")]
    public float fallDeathY = -10f;
    public float noReturnBuffer = 6f;

    [Header("Crouch")]
    public float standHeight = 2f;
    public float crouchHeight = 1f;
    public float crouchSpeedMultiplier = 0.5f;

    [Header("Extra Jump Ability (Q)")]
    // Q grants one on-demand jump on a fixed cooldown - usable even after the normal double jump is
    // spent, so it rescues a mistimed jump without breaking the (jump-safety-clamped) level design.
    public float extraJumpHeight = 2.4f;
    public float extraJumpCooldown = 20f;

    [Header("Dash")]
    public float dashSpeed = 18f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 1.5f;

    [Header("References")]
    public Transform cameraTransform;
    public Animator animator;
    public Camera playerCamera;
    public AudioListener audioListener;

    public float VerticalVelocity => velocity.y;
    public bool IsCrouching => isCrouching;
    public bool AbilityReady => cooldownRemaining <= 0f;
    public float CooldownRemaining => Mathf.Max(0f, cooldownRemaining);
    public Vector3 CheckpointPosition => checkpoint;

    private CharacterController controller;
    private Vector3 velocity;
    private int jumpsUsed;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float highestY;
    private bool isCrouching;
    private float cooldownRemaining;
    private float currentSpeed;
    private Vector3 horizontalVel;
    private bool wasAbilityReady = true;
    private float abilityReadyFlash;
    private Vector3 spawnPoint;
    private Vector3 checkpoint;
    private bool wasGrounded;
    private float footstepTimer;
    private const float FootstepInterval = 0.35f;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector3 dashDirection;
    private IMovingSurface currentPlatform;
    private bool adminFlyEnabled;
    private bool adminInvincible;
    private float adminSpeedMultiplier = 1f;

    public void SetCurrentPlatform(IMovingSurface platform)
    {
        currentPlatform = platform;
    }

    public void ClearCurrentPlatform(IMovingSurface platform)
    {
        if (currentPlatform == platform)
            currentPlatform = null;
    }

    public void SetAdminFly(bool enabled) => adminFlyEnabled = enabled;
    public void SetAdminInvincible(bool enabled) => adminInvincible = enabled;
    public void SetAdminSpeedMultiplier(float multiplier) => adminSpeedMultiplier = multiplier;

    public void AdminTeleport(Vector3 pos)
    {
        controller.enabled = false;
        transform.position = pos;
        controller.enabled = true;
        velocity = Vector3.zero;
    }

    public void AdminRespawn() => RespawnAtCheckpoint();

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        spawnPoint = transform.position;
        checkpoint = spawnPoint;
        highestY = transform.position.y;
        controller.height = standHeight;
        controller.center = new Vector3(0f, standHeight / 2f, 0f);
    }

    public void SetCheckpoint(Vector3 pos)
    {
        if (pos.y > checkpoint.y)
            checkpoint = pos;
    }

    public override void OnNetworkSpawn()
    {
        bool owner = IsOwner;

        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(owner);
            if (owner)
                playerCamera.transform.SetParent(null, true);
        }
        if (audioListener != null)
            audioListener.enabled = owner;

        if (owner)
        {
            cameraTransform = playerCamera != null ? playerCamera.transform : null;
            LobbyBootstrap.HideLobbyCamera();
            MainMenu.NotifyGameStarted();
            TutorialOverlay.ShowIfFirstTime();
            if (GameManager.Instance != null)
                GameManager.Instance.player = transform;

            playerName.Value = PlayerProfile.Name;

            SaveData save = SaveSystem.Load();
            if (save != null)
            {
                checkpoint = save.checkpointPosition;
                controller.enabled = false;
                transform.position = checkpoint;
                controller.enabled = true;
                highestY = checkpoint.y;
                GameManager.Instance?.SetCoinCount(save.coinCount);
            }
        }
    }

    void Update()
    {
        if (!IsOwner)
        {
            ApplyRemoteAnimation();
            return;
        }

        if (MainMenu.IsBlockingGameplay || WinScreen.HasWon || TutorialOverlay.IsVisible)
            return;

        float posY = transform.position.y;
        if (posY > highestY)
            highestY = posY;

        // The "Only-Up" no-return death punishes falling back down - but a moving/swinging platform
        // can carry you below the threshold through its own motion, which isn't the player's fault.
        // Suppress that death while riding one; the absolute void death (fallDeathY) still applies.
        bool carried = currentPlatform != null;
        if ((!carried && posY < highestY - noReturnBuffer) || posY < fallDeathY)
        {
            Die();
            if (!adminInvincible)
                return;
        }

        HandleCrouch();
        HandleGroundState();
        HandleJumpInput();
        HandleExtraJump();
        HandleDash();

        if (dashTimer <= 0f)
            HandleMovement();

        if (adminFlyEnabled && AdminAuth.IsAdmin)
            HandleAdminFly();
        else
            ApplyGravity();

        ApplyPlatformMotion();
        HandleAnimation();

        // Ability-ready feedback: chime + brief flash the moment the extra jump finishes cooling down.
        bool ready = AbilityReady;
        if (ready && !wasAbilityReady)
        {
            AudioManager.Instance?.PlayAbilityReady();
            abilityReadyFlash = 1f;
        }
        wasAbilityReady = ready;
        if (abilityReadyFlash > 0f)
            abilityReadyFlash = Mathf.MoveTowards(abilityReadyFlash, 0f, Time.deltaTime * 1.4f);
    }

    void HandleAdminFly()
    {
        float vertical = 0f;
        if (Input.GetKey(KeyCode.Space))
            vertical += 1f;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C))
            vertical -= 1f;

        const float adminFlySpeed = 9f;
        velocity.y = vertical * adminFlySpeed;
        controller.Move(new Vector3(0f, velocity.y * Time.deltaTime, 0f));
    }

    void ApplyPlatformMotion()
    {
        if (currentPlatform != null)
            controller.Move(currentPlatform.FrameDelta);
    }

    public void Die()
    {
        if (adminInvincible)
            return;

        RespawnAtCheckpoint();
    }

    void RespawnAtCheckpoint()
    {
        AudioManager.Instance?.PlayDeath();
        EffectsManager.Instance?.PlayDust(transform.position);
        currentPlatform = null;
        controller.enabled = false;
        transform.position = checkpoint;
        controller.enabled = true;
        velocity = Vector3.zero;
        horizontalVel = Vector3.zero;
        highestY = checkpoint.y;
    }

    // Reset the run to the very start (used by "Neu starten"): clears the checkpoint back to
    // the spawn and teleports there, without touching the network session.
    public void RestartFromBeginning()
    {
        checkpoint = spawnPoint;
        currentPlatform = null;
        controller.enabled = false;
        transform.position = spawnPoint;
        controller.enabled = true;
        velocity = Vector3.zero;
        horizontalVel = Vector3.zero;
        highestY = spawnPoint.y;
    }

    Vector3 GetWorldInputDir()
    {
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");
        Vector3 inputDir = new Vector3(inputX, 0f, inputZ).normalized;
        if (inputDir.magnitude < 0.1f)
            return Vector3.zero;

        float camYaw = cameraTransform != null ? cameraTransform.eulerAngles.y : 0f;
        return Quaternion.Euler(0f, camYaw, 0f) * inputDir;
    }

    void HandleDash()
    {
        if (dashCooldownTimer > 0f)
            dashCooldownTimer -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.LeftShift) && dashCooldownTimer <= 0f && dashTimer <= 0f)
        {
            Vector3 dir = GetWorldInputDir();
            dashDirection = dir.magnitude > 0.1f ? dir : transform.forward;
            dashTimer = dashDuration;
            dashCooldownTimer = dashCooldown;
            EffectsManager.Instance?.PlayDust(transform.position);
            AudioManager.Instance?.PlayWhoosh();
        }

        if (dashTimer > 0f)
        {
            dashTimer -= Time.deltaTime;
            controller.Move(dashDirection * dashSpeed * Time.deltaTime);
        }
    }

    void HandleAnimation()
    {
        netSpeed.Value = currentSpeed;
        netGrounded.Value = controller.isGrounded;
        netCrouching.Value = isCrouching;
        netVerticalVelocity.Value = velocity.y;

        if (animator == null)
            return;

        animator.SetFloat("Speed", currentSpeed);
        animator.SetBool("Grounded", controller.isGrounded);
        animator.SetBool("Crouching", isCrouching);
        animator.SetFloat("VerticalVelocity", velocity.y);
    }

    void ApplyRemoteAnimation()
    {
        if (animator == null)
            return;

        animator.SetFloat("Speed", netSpeed.Value);
        animator.SetBool("Grounded", netGrounded.Value);
        animator.SetBool("Crouching", netCrouching.Value);
        animator.SetFloat("VerticalVelocity", netVerticalVelocity.Value);
    }

    void HandleCrouch()
    {
        bool wantsCrouch = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);

        // Don't stand up into a low ceiling - the capsule would otherwise pop to full height and
        // clip/eject the character through the geometry above. Stay crouched until there's room.
        if (!wantsCrouch && isCrouching && CeilingBlocksStanding())
            wantsCrouch = true;

        isCrouching = wantsCrouch;
        float targetHeight = isCrouching ? crouchHeight : standHeight;
        controller.height = targetHeight;
        controller.center = new Vector3(0f, targetHeight / 2f, 0f);
    }

    bool CeilingBlocksStanding()
    {
        // Cast from the crouched capsule's top up to standing height, ignoring the player layer (8).
        Vector3 origin = transform.position + Vector3.up * crouchHeight;
        float dist = Mathf.Max(0.05f, standHeight - crouchHeight);
        return Physics.SphereCast(origin, controller.radius * 0.9f, Vector3.up, out _, dist,
            ~(1 << 8), QueryTriggerInteraction.Ignore);
    }

    void HandleExtraJump()
    {
        if (cooldownRemaining > 0f)
            cooldownRemaining -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Q) && cooldownRemaining <= 0f)
        {
            // On-demand extra jump: an upward impulse that works even mid-air after the double jump
            // is spent. Fixed cooldown so it's a rescue, not a spammable flight.
            velocity.y = Mathf.Sqrt(extraJumpHeight * -2f * gravity);
            cooldownRemaining = extraJumpCooldown;
            AudioManager.Instance?.PlayWhoosh();
            EffectsManager.Instance?.PlaySparkle(transform.position);
            EffectsManager.Instance?.PlayDust(transform.position);
        }
    }

    void OnGUI()
    {
        if (!IsOwner || MainMenu.IsBlockingGameplay || WinScreen.HasWon)
            return;

        UITheme.EnsureInit();

        // Ability chip below the GameManager HUD (which occupies y≈16..178): icon + label, a
        // right-aligned state ("Bereit"/"Xs"), and a recharge bar so the cooldown is readable at a
        // glance. Pulses/brightens the moment it becomes ready.
        bool ready = AbilityReady;
        float frac = ready ? 1f : Mathf.Clamp01(1f - CooldownRemaining / Mathf.Max(0.01f, extraJumpCooldown));
        Color accent = ready ? UITheme.Positive : UITheme.Accent;
        if (abilityReadyFlash > 0f)
            accent = Color.Lerp(accent, Color.white, abilityReadyFlash * 0.7f);

        Rect chip = new Rect(18, 188, 214, 44);
        GUI.Box(chip, "", UITheme.PillStyle);

        float pulse = ready ? (1f + Mathf.Sin(Time.time * 4f) * 0.14f) : 1f;
        float iconSize = 16f * pulse;
        UITheme.Rect(new Rect(chip.x + 12 + (16f - iconSize) / 2f, chip.y + 8 + (16f - iconSize) / 2f, iconSize, iconSize), accent);

        var labelStyle = new GUIStyle(UITheme.LabelStyle) { fontSize = 14, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(chip.x + 36, chip.y + 5, 130, 20), "Extra-Sprung (Q)", labelStyle);

        var stateStyle = new GUIStyle(UITheme.LabelStyle) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        stateStyle.normal.textColor = accent;
        GUI.Label(new Rect(chip.xMax - 74, chip.y + 5, 62, 20), ready ? "Bereit" : $"{CooldownRemaining:0}s", stateStyle);

        UITheme.Bar(new Rect(chip.x + 12, chip.y + 28, chip.width - 24, 8), frac, accent);
    }

    void HandleGroundState()
    {
        if (controller.isGrounded)
        {
            coyoteTimer = coyoteTime;
            jumpsUsed = 0;
            if (velocity.y < 0f)
                velocity.y = -2f;

            if (!wasGrounded)
            {
                AudioManager.Instance?.PlayLand();
                EffectsManager.Instance?.PlayDust(transform.position);
            }

            if (currentSpeed > 0.1f)
            {
                footstepTimer -= Time.deltaTime;
                if (footstepTimer <= 0f)
                {
                    AudioManager.Instance?.PlayFootstep();
                    footstepTimer = FootstepInterval;
                }
            }
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
            // Walking off a ledge without jumping shouldn't grant a fresh full double-jump. Once the
            // coyote grace elapses unused, consume the grounded jump so only the air jump remains.
            if (coyoteTimer <= 0f && jumpsUsed == 0)
                jumpsUsed = 1;
        }

        wasGrounded = controller.isGrounded;
        jumpBufferTimer -= Time.deltaTime;
    }

    void HandleJumpInput()
    {
        if (Input.GetButtonDown("Jump"))
            jumpBufferTimer = jumpBufferTime;

        if (jumpBufferTimer <= 0f)
            return;

        bool canJump = coyoteTimer > 0f || jumpsUsed < maxJumps;
        if (canJump)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpsUsed++;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            AudioManager.Instance?.PlayJump();
            EffectsManager.Instance?.PlayDust(transform.position);
        }
    }

    void HandleMovement()
    {
        Vector3 moveDir = GetWorldInputDir();
        bool hasInput = moveDir.magnitude >= 0.1f;

        if (hasInput)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Ease horizontal velocity toward the target instead of snapping. A hard reversal now
        // decelerates through zero and accelerates back out, so direction changes feel controlled
        // rather than twitchy - while the high accel keeps it responsive.
        float speed = (isCrouching ? moveSpeed * crouchSpeedMultiplier : moveSpeed) * adminSpeedMultiplier;
        Vector3 targetVel = hasInput ? moveDir * speed : Vector3.zero;
        float rate = hasInput ? moveAcceleration : moveDeceleration;
        horizontalVel = Vector3.MoveTowards(horizontalVel, targetVel, rate * Time.deltaTime);

        controller.Move(horizontalVel * Time.deltaTime);
        currentSpeed = horizontalVel.magnitude;
    }

    void ApplyGravity()
    {
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    public void ApplyBounce(float force)
    {
        velocity.y = force;
        jumpsUsed = 0;
    }
}
