using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 7f;
    public float rotationSpeed = 12f;

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

    [Header("Fly Ability (Q)")]
    public float flyDuration = 3f;
    public float flySpeed = 6f;
    public float baseCooldown = 180f;
    public float cooldownReductionPerShard = 30f;
    public float minCooldown = 30f;

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
    public bool IsFlying => isFlying;
    public bool AbilityReady => cooldownRemaining <= 0f;
    public float CooldownRemaining => Mathf.Max(0f, cooldownRemaining);

    private CharacterController controller;
    private Vector3 velocity;
    private int jumpsUsed;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float highestY;
    private bool isCrouching;
    private bool isFlying;
    private float flyTimer;
    private float cooldownRemaining;
    private float currentSpeed;
    private Vector3 spawnPoint;
    private Vector3 checkpoint;
    private bool wasGrounded;
    private float footstepTimer;
    private const float FootstepInterval = 0.35f;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector3 dashDirection;

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
        }
    }

    void Update()
    {
        if (!IsOwner || MainMenu.IsBlockingGameplay || WinScreen.HasWon || TutorialOverlay.IsVisible)
            return;

        float posY = transform.position.y;
        if (posY > highestY)
            highestY = posY;

        if (posY < highestY - noReturnBuffer || posY < fallDeathY)
        {
            Die();
            return;
        }

        HandleCrouch();
        HandleGroundState();
        HandleJumpInput();
        HandleFlyAbility();
        HandleDash();

        if (dashTimer <= 0f)
            HandleMovement();

        ApplyGravity();
        HandleAnimation();
    }

    public void Die()
    {
        AudioManager.Instance?.PlayDeath();
        EffectsManager.Instance?.PlayDust(transform.position);
        controller.enabled = false;
        transform.position = checkpoint;
        controller.enabled = true;
        velocity = Vector3.zero;
        highestY = checkpoint.y;
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
        }

        if (dashTimer > 0f)
        {
            dashTimer -= Time.deltaTime;
            controller.Move(dashDirection * dashSpeed * Time.deltaTime);
        }
    }

    void HandleAnimation()
    {
        if (animator == null)
            return;

        animator.SetFloat("Speed", currentSpeed);
        animator.SetBool("Grounded", controller.isGrounded);
        animator.SetBool("Crouching", isCrouching);
        animator.SetFloat("VerticalVelocity", velocity.y);
    }

    void HandleCrouch()
    {
        isCrouching = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
        float targetHeight = isCrouching ? crouchHeight : standHeight;
        controller.height = targetHeight;
        controller.center = new Vector3(0f, targetHeight / 2f, 0f);
    }

    void HandleFlyAbility()
    {
        if (cooldownRemaining > 0f)
            cooldownRemaining -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Q) && !isFlying && cooldownRemaining <= 0f)
        {
            isFlying = true;
            flyTimer = flyDuration;
            int shards = GameManager.Instance != null ? GameManager.Instance.CoinCount : 0;
            cooldownRemaining = Mathf.Max(minCooldown, baseCooldown - shards * cooldownReductionPerShard);
        }

        if (isFlying)
        {
            flyTimer -= Time.deltaTime;
            velocity.y = flySpeed;
            jumpsUsed = 0;
            if (flyTimer <= 0f)
                isFlying = false;
        }
    }

    void OnGUI()
    {
        if (!IsOwner || MainMenu.IsBlockingGameplay || WinScreen.HasWon)
            return;

        UITheme.EnsureInit();
        string status = AbilityReady ? "Flug (Q): bereit" : $"Flug (Q): {CooldownRemaining:0}s";
        GUI.Label(new Rect(20, 108, 300, 30), status, UITheme.HudStyle);
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

        if (moveDir.magnitude >= 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            float speed = isCrouching ? moveSpeed * crouchSpeedMultiplier : moveSpeed;
            controller.Move(moveDir * speed * Time.deltaTime);
            currentSpeed = speed;
        }
        else
        {
            currentSpeed = 0f;
        }
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
