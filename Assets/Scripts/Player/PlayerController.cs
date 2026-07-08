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

    // The HOST's game mode, replicated to every peer so everyone plays the same world (the
    // Endless extension toggles whole chunks of the tower on/off). The server stamps this on
    // every player object; each client reads it back from its own and applies it locally.
    private readonly NetworkVariable<int> netGameMode = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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
    // How far below your highest-reached height you may fall before the Only-Up reset. Generous so a
    // missed jump that drops you several platforms can still be recovered on a ledge below, instead of
    // snapping you back to the checkpoint too eagerly (the absolute void death still applies).
    public float noReturnBuffer = 18f;

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

    [Header("Slide")]
    // Crouch while running: a short slide with a speed boost. Jumping OUT of a slide keeps the
    // momentum for a moment, so chaining slide-jumps rewards clean movement (bhop-light).
    public float slideBoost = 1.45f;
    public float slideDuration = 0.85f;
    public float slideMinSpeed = 4.5f;
    private float slideTimer;
    private Vector3 slideDir;
    private float momentumTimer; // after a slide-jump: keep the boosted speed briefly
    private bool SlideActive => slideTimer > 0f;

    // Updraft (WindColumn) state: while fed, gravity is replaced by a rising force.
    private float updraftTimer;
    private float updraftLift;
    private float updraftMax;

    // Ice: standing on an IceSurface makes accel/decel slippery.
    private bool onIce;

    private float ledgeAssistCooldown;

    // Dash-trail defaults, cached before the first action-effect tint is applied.
    private bool dashTrailDefaultCached;
    private Color dashTrailDefaultColor = Color.white;
    private float dashTrailDefaultWidth = 1f;

    // Zipline ride state (see Zipline.cs): while riding, normal movement/gravity are suspended
    // and the player hangs below the cable, sliding from start to end. Space bails out early.
    private bool ridingZipline;
    private Vector3 zipFrom, zipTo;
    private float zipProgress, zipSpeed;
    private float zipCooldown;

    [Header("Glide")]
    // Hold Jump while airborne and descending to glide: gravity eases to a gentle terminal descent so
    // you can steer across long gaps and drift down from the floating night islands. Tunable feel.
    public bool glideEnabled = true;
    public float glideFallSpeed = -3.4f;  // gentle terminal descent while gliding
    public float glideEaseRate = 9f;      // how fast the fall eases toward that terminal
    private bool isGliding;

    [Header("Combat")]
    public GameObject arrowPrefab;         // ranged bow arrow (wired by SceneBuilder)
    public Transform bowVisual;            // the drawn bow, shown only while equipped (wired by SceneBuilder)
    public float attackCooldown = 0.5f;
    private float attackTimer;
    private bool bowEquipped;
    // Crosshair sits ABOVE screen centre so it clears the third-person character's head; the shot aims
    // through this same point. Fraction is measured from the top of the screen.
    private const float CrosshairYFrac = 0.36f;

    [Header("References")]
    public Transform cameraTransform;
    // Movement uses this camera's non-lagging orbit yaw (see GetWorldInputDir) to avoid the
    // camera-lag feedback loop that curved WASD movement into a circle.
    private CameraFollow cameraFollow;
    public Animator animator;
    public Camera playerCamera;
    public AudioListener audioListener;
    public TrailRenderer dashTrail; // motion streak enabled only while dashing (wired by SceneBuilder)

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

        // Stamp the authoritative mode immediately so joining clients receive it with the spawn.
        if (IsServer)
            netGameMode.Value = (int)GameModeState.Current;

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
            cameraFollow = playerCamera != null ? playerCamera.GetComponent<CameraFollow>() : null;
            LobbyBootstrap.HideLobbyCamera();
            MainMenu.NotifyGameStarted();
            TutorialOverlay.ShowIfFirstTime();
            if (GameManager.Instance != null)
                GameManager.Instance.player = transform;

            playerName.Value = PlayerProfile.Name;

            SaveData save = SaveSystem.Load();
            if (save != null && save.hasRun) // records-only saves (after "Neues Spiel") aren't runs
            {
                // Restore the mode this run was saved in FIRST: an Endless checkpoint above the
                // base tower only has ground under it once the Endless extension is switched on.
                GameModeState.Current = (GameMode)save.gameMode;
                FindFirstObjectByType<ModeWorld>()?.ForceApply();

                Vector3 target = save.checkpointPosition;
                // Safety net: if there is no ground beneath the saved checkpoint (older save,
                // changed world), resuming would fall-respawn-loop forever - use the spawn instead.
                if (!Physics.Raycast(target + Vector3.up * 2f, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore))
                    target = spawnPoint;

                checkpoint = target;
                controller.enabled = false;
                transform.position = checkpoint;
                controller.enabled = true;
                highestY = checkpoint.y;
                GameManager.Instance?.SetCoinCount(save.coinCount);
            }

            // Joined as a guest: the host's mode (delivered with the spawn) overrides whatever the
            // local save restored, so we never stand in a world the host doesn't have active.
            if (!IsServer)
                ApplyServerGameMode();
        }
    }

    // Client-side: adopt the host's replicated mode. If that toggles the Endless extension under
    // our feet (guest resumed an Endless checkpoint into a Klassisch host), fall back to the spawn
    // instead of fall-respawn-looping on an orphaned checkpoint.
    void ApplyServerGameMode()
    {
        GameMode incoming = (GameMode)netGameMode.Value;
        if (incoming == GameModeState.Current)
            return;

        GameModeState.Current = incoming;
        FindFirstObjectByType<ModeWorld>()?.ForceApply();
        if (!Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore))
            RestartFromBeginning();
    }

    void Update()
    {
        // Server stamps the authoritative mode on EVERY player object (cheap no-op once equal).
        if (IsServer && netGameMode.Value != (int)GameModeState.Current)
            netGameMode.Value = (int)GameModeState.Current;

        if (!IsOwner)
        {
            ApplyRemoteAnimation();
            return;
        }

        // Clients follow the host's mode - checked before the menu early-out so the world stays
        // consistent even while a menu is open.
        if (!IsServer)
            ApplyServerGameMode();

        if (MainMenu.IsBlockingGameplay || WinScreen.HasWon || TutorialOverlay.IsVisible || PhotoMode.Active)
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

        // Zipline ride replaces normal movement entirely until release/arrival.
        if (ridingZipline)
        {
            UpdateZipline();
            HandleAnimation();
            return;
        }
        if (zipCooldown > 0f)
            zipCooldown -= Time.deltaTime;

        HandleZiplineUse();
        HandleCrouch();
        HandleGroundState();
        HandleSlide();
        HandleJumpInput();
        HandleExtraJump();
        HandleDash();
        HandleBowToggle();
        HandleAttack();
        HandleLedgeAssist();

        if (dashTimer <= 0f)
            HandleMovement();

        UpdateGlide();
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

        GameManager.Instance?.RegisterDeath();
        RespawnAtCheckpoint();

        // Hardcore: ONE life - the first death ends the run (after the respawn teleport, so the
        // camera isn't left falling into the void behind the game-over screen).
        if (GameModeState.Current == GameMode.Hardcore)
            WinScreen.Instance?.TriggerGameOver();
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
        slideTimer = 0f;
        momentumTimer = 0f;
        ridingZipline = false;
        // Rematerialize with a sparkle so the respawn reads as an event, not a teleport glitch.
        EffectsManager.Instance?.PlaySparkle(checkpoint + Vector3.up * 1f);
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

        // Use the camera's intended orbit yaw (lag-free). Falling back to the transform's eulerAngles.y
        // would reintroduce the camera-lag feedback loop that spun the player in a circle.
        float camYaw = cameraFollow != null ? cameraFollow.Yaw
                     : (cameraTransform != null ? cameraTransform.eulerAngles.y : 0f);
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
            if (dashTrail != null)
            {
                // Equipped action-effect (shop) tints the dash streak - kept thin and translucent
                // so it stays a subtle accent; defaults cached once and restored when unequipped.
                if (!dashTrailDefaultCached)
                {
                    dashTrailDefaultColor = dashTrail.startColor;
                    dashTrailDefaultWidth = dashTrail.widthMultiplier;
                    dashTrailDefaultCached = true;
                }
                Color? tint = EffectsManager.FxTint;
                if (tint.HasValue)
                {
                    dashTrail.startColor = new Color(tint.Value.r, tint.Value.g, tint.Value.b, dashTrailDefaultColor.a * 0.55f);
                    dashTrail.widthMultiplier = dashTrailDefaultWidth * 0.5f;
                }
                else
                {
                    dashTrail.startColor = dashTrailDefaultColor;
                    dashTrail.widthMultiplier = dashTrailDefaultWidth;
                }
                dashTrail.Clear();
                dashTrail.emitting = true;
            }
        }

        if (dashTimer > 0f)
        {
            dashTimer -= Time.deltaTime;
            controller.Move(dashDirection * dashSpeed * Time.deltaTime);
            if (dashTimer <= 0f && dashTrail != null)
                dashTrail.emitting = false; // stop feeding; the streak fades out on its own
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
        bool wantsCrouch = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C) || SlideActive;

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

    // Cached HUD styles - building them fresh every OnGUI frame was steady GC garbage.
    static GUIStyle hintLabelStyle, hintSubStyle;

    // A compact key-hint chip (accent bar + action + key) for the bottom HUD row.
    static void DrawHintChip(Rect r, Color accent, string label, string sub)
    {
        if (hintLabelStyle == null)
        {
            hintLabelStyle = new GUIStyle(UITheme.LabelStyle) { fontSize = 15, fontStyle = FontStyle.Bold };
            hintSubStyle = new GUIStyle(UITheme.LabelStyle) { fontSize = 11 };
            hintSubStyle.normal.textColor = UITheme.InkDim;
        }
        GUI.Box(r, "", UITheme.PillStyle);
        UITheme.RoundRect(new Rect(r.x + 12f, r.y + r.height * 0.5f - 10f, 6f, 20f), accent);
        GUI.Label(new Rect(r.x + 26f, r.y + 8f, r.width - 32f, 20f), label, hintLabelStyle);
        GUI.Label(new Rect(r.x + 26f, r.y + 31f, r.width - 32f, 18f), sub, hintSubStyle);
    }

    void OnGUI()
    {
        UITheme.BeginUI();
        if (!IsOwner || MainMenu.IsBlockingGameplay || WinScreen.HasWon || PhotoMode.Active)
            return;

        UITheme.EnsureInit();

        // Ability chip centered along the bottom of the screen so it's easy to see at a glance:
        // icon + label, a right-aligned state ("Bereit"/"Xs"), and a recharge bar for the cooldown.
        // Pulses/brightens the moment it becomes ready.
        bool ready = AbilityReady;
        float frac = ready ? 1f : Mathf.Clamp01(1f - CooldownRemaining / Mathf.Max(0.01f, extraJumpCooldown));
        Color accent = ready ? UITheme.Positive : UITheme.Accent;
        if (abilityReadyFlash > 0f)
            accent = Color.Lerp(accent, Color.white, abilityReadyFlash * 0.7f);

        // Bottom row: FOUR chips of IDENTICAL size, one shared format (accent bar + action + key).
        // The Extra-Sprung chip additionally carries a slim recharge bar along its bottom edge.
        const float chipW = 190f, chipH = 58f, gap = 8f;
        float rowW = chipW * 4f + gap * 3f;
        float rowX = (UITheme.ScreenW - rowW) * 0.5f;
        float y0 = UITheme.ScreenH - chipH - 24f;

        DrawHintChip(new Rect(rowX, y0, chipW, chipH), UITheme.Accent, "Gleiten", "Leertaste halten");

        Rect ejChip = new Rect(rowX + (chipW + gap), y0, chipW, chipH);
        DrawHintChip(ejChip, accent, "Extra-Sprung", ready ? "Q · Bereit" : $"Q · {CooldownRemaining:0}s");
        UITheme.Bar(new Rect(ejChip.x + 26f, ejChip.y + chipH - 9f, ejChip.width - 40f, 4f), frac, accent);

        DrawHintChip(new Rect(rowX + (chipW + gap) * 2f, y0, chipW, chipH),
            bowEquipped ? UITheme.Positive : UITheme.Accent, "Bogen",
            bowEquipped ? "Gezogen · L-Klick" : "Mausrad ziehen");
        DrawHintChip(new Rect(rowX + (chipW + gap) * 3f, y0, chipW, chipH), UITheme.Gold, "Shop", "Tab");

        // Zipline prompt while standing at a handle: one clear "press E" chip above the HUD row.
        if (ZiplineOffered && !ridingZipline)
            DrawHintChip(new Rect((UITheme.ScreenW - chipW) * 0.5f, y0 - chipH - 10f, chipW, chipH),
                UITheme.Positive, "Zipline", "E — einhängen");

        // Bow aiming crosshair (screen centre) - only while the bow is drawn.
        if (bowEquipped)
        {
            float cx = UITheme.ScreenW * 0.5f, cy = UITheme.ScreenH * CrosshairYFrac;
            Color ch = new Color(1f, 1f, 1f, 0.6f);
            UITheme.Rect(new Rect(cx - 1f, cy - 9f, 2f, 6f), ch);
            UITheme.Rect(new Rect(cx - 1f, cy + 3f, 2f, 6f), ch);
            UITheme.Rect(new Rect(cx - 9f, cy - 1f, 6f, 2f), ch);
            UITheme.Rect(new Rect(cx + 3f, cy - 1f, 6f, 2f), ch);
        }
    }

    void HandleGroundState()
    {
        if (controller.isGrounded)
        {
            coyoteTimer = coyoteTime;
            jumpsUsed = 0;
            float impactSpeed = velocity.y < 0f ? -velocity.y : 0f;
            if (velocity.y < 0f)
                velocity.y = -2f;

            if (!wasGrounded)
            {
                AudioManager.Instance?.PlayLand();
                EffectsManager.Instance?.PlayLandingDust(transform.position, impactSpeed);
            }

            if (currentSpeed > 0.1f)
            {
                footstepTimer -= Time.deltaTime;
                if (footstepTimer <= 0f)
                {
                    AudioManager.Instance?.PlayFootstep();
                    footstepTimer = FootstepInterval;
                    // A small dust puff per stride at running speed grounds the movement visually.
                    if (currentSpeed > 4.5f)
                        EffectsManager.Instance?.PlayDust(transform.position);
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
            bool airJump = coyoteTimer <= 0f && jumpsUsed >= 1; // a mid-air (double) jump
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpsUsed++;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            // Slide-jump: leaving the ground mid-slide carries the boosted speed into the air.
            if (SlideActive)
            {
                slideTimer = 0f;
                momentumTimer = 0.65f;
            }
            AudioManager.Instance?.PlayJump();
            if (airJump)
                EffectsManager.Instance?.PlayJumpRing(transform.position);
            else
                EffectsManager.Instance?.PlayDust(transform.position);
        }
    }

    // Crouch while running fast on the ground -> a short boosted slide. Jumping during the slide
    // hands the boosted speed to momentumTimer so it survives into the air (slide-jump chains).
    void HandleSlide()
    {
        if (slideTimer > 0f)
        {
            slideTimer -= Time.deltaTime;
            if (!controller.isGrounded || currentSpeed < 2f)
                slideTimer = 0f; // slid off an edge or scrubbed all speed
        }

        bool crouchPressed = Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C);
        if (crouchPressed && controller.isGrounded && !SlideActive && currentSpeed > slideMinSpeed && dashTimer <= 0f)
        {
            slideTimer = slideDuration;
            slideDir = horizontalVel.normalized;
            EffectsManager.Instance?.PlayDust(transform.position);
            AudioManager.Instance?.PlayWhoosh();
        }

        if (momentumTimer > 0f)
            momentumTimer -= Time.deltaTime;
    }

    // Ledge assist: a barely-missed jump toward a platform edge pops you up over the lip instead
    // of sliding off - invisible QoL that removes the most common "so close!" frustration.
    void HandleLedgeAssist()
    {
        if (ledgeAssistCooldown > 0f)
            ledgeAssistCooldown -= Time.deltaTime;
        if (controller.isGrounded || velocity.y > 1.5f || isGliding || ledgeAssistCooldown > 0f)
            return;

        Vector3 fwd = horizontalVel.sqrMagnitude > 1f ? horizontalVel.normalized : transform.forward;
        Vector3 feet = transform.position + Vector3.up * 0.25f;
        if (!Physics.Raycast(feet, fwd, out RaycastHit wall, 0.8f, ~(1 << 8), QueryTriggerInteraction.Ignore))
            return;

        // Look for the wall's top surface just above us: close enough to boost over.
        Vector3 over = feet + Vector3.up * 1.25f + fwd * (wall.distance + 0.35f);
        if (Physics.Raycast(over, Vector3.down, out RaycastHit top, 1.3f, ~(1 << 8), QueryTriggerInteraction.Ignore)
            && top.point.y > transform.position.y + 0.15f
            && top.point.y < transform.position.y + 1.25f)
        {
            velocity.y = Mathf.Sqrt(1.15f * -2f * gravity); // small pop, just enough to clear the lip
            ledgeAssistCooldown = 0.9f;
            EffectsManager.Instance?.PlayDust(transform.position);
        }
    }

    // A zipline handle in range keeps offering itself (OnTriggerStay); the player accepts with E.
    private Zipline nearbyZipline;
    private float ziplineOfferTime = -9f;
    private bool ZiplineOffered => nearbyZipline != null && Time.time - ziplineOfferTime < 0.25f;

    public void OfferZipline(Zipline zip)
    {
        nearbyZipline = zip;
        ziplineOfferTime = Time.time;
    }

    void HandleZiplineUse()
    {
        if (ZiplineOffered && !ridingZipline && Input.GetKeyDown(KeyCode.E))
            TryStartZipline(nearbyZipline.transform.position, nearbyZipline.endPoint, nearbyZipline.speed);
    }

    // Grab a zipline (accepted with E while in range of a handle). Refuses while already riding or
    // right after a release, so bailing out doesn't instantly re-grab.
    public bool TryStartZipline(Vector3 from, Vector3 to, float speed)
    {
        if (ridingZipline || zipCooldown > 0f)
            return false;
        ridingZipline = true;
        zipFrom = from;
        zipTo = to;
        zipSpeed = speed;
        zipProgress = 0f;
        velocity = Vector3.zero;
        horizontalVel = Vector3.zero;
        slideTimer = 0f;
        AudioManager.Instance?.PlayWhoosh();
        EffectsManager.Instance?.PlaySparkle(from);
        return true;
    }

    void UpdateZipline()
    {
        float dist = Mathf.Max(1f, Vector3.Distance(zipFrom, zipTo));
        zipProgress += (zipSpeed / dist) * Time.deltaTime;
        Vector3 hang = Vector3.Lerp(zipFrom, zipTo, Mathf.Clamp01(zipProgress)) + Vector3.down * 1.7f;
        controller.Move(hang - transform.position);

        Vector3 along = zipTo - zipFrom;
        along.y = 0f;
        if (along.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(along.normalized), 8f * Time.deltaTime);

        // Space = bail out early; arrival releases automatically - both with a small hop plus the
        // ride's momentum, so the dismount flows into the next jump.
        if (Input.GetButtonDown("Jump") || zipProgress >= 1f)
        {
            ridingZipline = false;
            zipCooldown = 1.2f;
            velocity = Vector3.zero;
            velocity.y = Mathf.Sqrt(1.2f * -2f * gravity);
            horizontalVel = along.sqrMagnitude > 0.01f ? along.normalized * Mathf.Min(zipSpeed, 8f) : Vector3.zero;
            momentumTimer = 0.4f;
            jumpsUsed = 1; // the air jump stays available for the landing
            EffectsManager.Instance?.PlayDust(transform.position);
            AudioManager.Instance?.PlayJump();
        }
    }

    // Fed by WindColumn triggers every physics tick while inside a rising column.
    public void ApplyUpdraft(float lift, float maxRise)
    {
        updraftTimer = 0.15f;
        updraftLift = lift;
        updraftMax = maxRise;
    }

    void HandleMovement()
    {
        Vector3 moveDir = GetWorldInputDir();
        bool hasInput = moveDir.magnitude >= 0.1f;

        if (hasInput)
        {
            Quaternion targetRotation = Quaternion.LookRotation(SlideActive ? slideDir : moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Standing on ice? Then acceleration/deceleration drop hard - momentum carries.
        onIce = controller.isGrounded
            && Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit ground, 0.5f,
                ~(1 << 8), QueryTriggerInteraction.Ignore)
            && ground.collider.GetComponentInParent<IceSurface>() != null;

        // Ease horizontal velocity toward the target instead of snapping. A hard reversal now
        // decelerates through zero and accelerates back out, so direction changes feel controlled
        // rather than twitchy - while the high accel keeps it responsive.
        float speed = (isCrouching && !SlideActive ? moveSpeed * crouchSpeedMultiplier : moveSpeed) * adminSpeedMultiplier;
        Vector3 targetVel = hasInput ? moveDir * speed : Vector3.zero;
        float rate = hasInput ? moveAcceleration : moveDeceleration;

        if (SlideActive)
        {
            // Slide: boosted speed along the slide direction, decaying over the slide; slight steering.
            slideDir = Vector3.Slerp(slideDir, hasInput ? moveDir : slideDir, 2.5f * Time.deltaTime).normalized;
            float boost = moveSpeed * slideBoost * Mathf.Lerp(0.7f, 1f, slideTimer / slideDuration);
            horizontalVel = slideDir * boost;
        }
        else
        {
            if (momentumTimer > 0f && hasInput)
                targetVel = moveDir * Mathf.Max(speed, horizontalVel.magnitude); // slide-jump carry
            if (onIce)
                rate *= 0.16f; // slippery: barely any grip
            horizontalVel = Vector3.MoveTowards(horizontalVel, targetVel, rate * Time.deltaTime);
        }

        controller.Move(horizontalVel * Time.deltaTime);
        currentSpeed = horizontalVel.magnitude;
    }

    void ApplyGravity()
    {
        if (updraftTimer > 0f)
        {
            // Rising wind: replaces gravity while inside the column - ride it up (great with glide).
            updraftTimer -= Time.deltaTime;
            velocity.y = Mathf.MoveTowards(velocity.y, updraftMax, updraftLift * Time.deltaTime);
        }
        else if (isGliding)
        {
            velocity.y = Mathf.MoveTowards(velocity.y, glideFallSpeed, glideEaseRate * Time.deltaTime);
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }
        controller.Move(velocity * Time.deltaTime);
    }

    // Glide state: holding Jump while airborne and descending eases the fall to a gentle terminal so
    // you can steer across gaps that a normal jump can't clear.
    void UpdateGlide()
    {
        isGliding = glideEnabled && !controller.isGrounded && velocity.y < 1.5f && Input.GetButton("Jump");
    }

    // Mouse wheel draws (up) / holsters (down) the bow. The crosshair and firing only work while it's
    // drawn, and the bow model shows in the player's hand.
    void HandleBowToggle()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0.01f) SetBow(true);
        else if (scroll < -0.01f) SetBow(false);
    }

    void SetBow(bool on)
    {
        if (bowEquipped == on)
            return;
        bowEquipped = on;
        if (bowVisual != null)
            bowVisual.gameObject.SetActive(on);
        AudioManager.Instance?.PlayWhoosh();
    }

    public bool BowEquipped => bowEquipped;

    // Left-click bow: fires an arrow toward the crosshair that damages the first enemy it hits
    // (2 hits kill a 40-HP wraith). Only works while the bow is drawn (mouse wheel up).
    void HandleAttack()
    {
        if (attackTimer > 0f)
            attackTimer -= Time.deltaTime;
        if (!bowEquipped || !Input.GetMouseButtonDown(0) || attackTimer > 0f || arrowPrefab == null)
            return;

        attackTimer = attackCooldown;
        AudioManager.Instance?.PlayWhoosh();

        Camera cam = Camera.main;
        Vector3 origin = transform.position + Vector3.up * 1.1f + transform.forward * 0.6f;
        Vector3 dir;
        if (cam != null)
        {
            // Aim at whatever the crosshair is over. Walk the ray's hits (nearest first), skip our own
            // body and pass-through triggers (coins), and lock onto the first enemy or solid surface.
            // Aiming straight at the enemy's centre means the shot converges on it despite third-person
            // parallax, so close shots don't fly wide.
            Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * (1f - CrosshairYFrac), 0f));
            Vector3 aim = ray.GetPoint(150f);
            var hits = Physics.RaycastAll(ray, 250f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hh in hits)
            {
                if (hh.collider.GetComponentInParent<PlayerController>() == this)
                    continue; // never aim at ourselves
                var en = hh.collider.GetComponentInParent<Enemy>();
                if (en != null) { aim = en.transform.position + Vector3.up * 0.8f; break; }
                if (!hh.collider.isTrigger) { aim = hh.point; break; }
                // other trigger (coin/checkpoint) - ignore and keep looking
            }
            dir = (aim - origin).normalized;
        }
        else
        {
            dir = transform.forward;
        }

        GameObject arrow = Instantiate(arrowPrefab, origin, Quaternion.LookRotation(dir));
        arrow.transform.SetParent(null); // never ride along with the player
        arrow.SetActive(true);
        EffectsManager.Instance?.PlaySparkle(origin);
    }

    public void ApplyBounce(float force)
    {
        velocity.y = force;
        jumpsUsed = 0;
    }
}
