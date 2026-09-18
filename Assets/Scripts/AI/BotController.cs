using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BotController : MonoBehaviour
{
    [Header("Bot Settings")]
    [SerializeField] private float moveSpeed = 6.0f;
    [SerializeField] private float rotationSpeed = 10.0f;
    [SerializeField] private float jumpForce = 8.5f;
    [SerializeField] private float forwardJumpBoost = 2.2f;
    [SerializeField] private float gapCheckDistance = 1.0f;
    [SerializeField] private float groundCheckDistance = 0.4f;
    [SerializeField] private float jumpCooldown = 0.4f;
    [SerializeField] private float reactionDelayMin = 0.1f;
    [SerializeField] private float reactionDelayMax = 0.35f;

    [Header("Dependencies")]
    [SerializeField] private CharacterSquashAndStretch squashAndStretch;

    private Rigidbody rb;
    private bool isGrounded = false;
    private bool isEliminated = false;
    private float lastJumpTime = -1f;
    private Vector3 currentMoveTarget;
    private PlatformTile targetTile;
    private float nextTargetEvaluationTime = 0f;
    private string botName = "Bot";
    private Material botMaterial;

    public string BotName => botName;
    public bool IsEliminated => isEliminated;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        if (squashAndStretch == null)
        {
            squashAndStretch = GetComponent<CharacterSquashAndStretch>();
        }
    }

    public void Initialize(string name, Color color)
    {
        botName = name;
        isEliminated = false;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        MeshRenderer renderer = GetComponentInChildren<MeshRenderer>();
        if (renderer != null)
        {
            Shader defaultShader = Shader.Find("Standard");
            Shader targetShader = (renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null) ? renderer.sharedMaterial.shader : defaultShader;
            botMaterial = new Material(targetShader);
            botMaterial.color = color;
            renderer.material = botMaterial;
        }
    }

    private void Update()
    {
        if (isEliminated) return;

        CheckGrounded();

        if (Time.time >= nextTargetEvaluationTime)
        {
            EvaluateAndPickTargetTile();
            nextTargetEvaluationTime = Time.time + Random.Range(reactionDelayMin, reactionDelayMax);
        }

        Vector3 moveInput = (currentMoveTarget - transform.position);
        moveInput.y = 0;

        if (moveInput.sqrMagnitude > 0.05f)
        {
            CheckAutoJump(moveInput.normalized);
        }

        // Fall elimination check
        if (transform.position.y < -5.0f && !isEliminated)
        {
            Eliminate();
        }
    }

    private void FixedUpdate()
    {
        if (isEliminated) return;

        Vector3 moveDir = (currentMoveTarget - transform.position);
        moveDir.y = 0;

        if (moveDir.sqrMagnitude > 0.05f)
        {
            moveDir.Normalize();

            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);

            Vector3 targetVelocity = moveDir * moveSpeed;
            targetVelocity.y = rb.velocity.y;
            rb.velocity = targetVelocity;
        }
        else if (isGrounded)
        {
            rb.velocity = new Vector3(rb.velocity.x * 0.7f, rb.velocity.y, rb.velocity.z * 0.7f);
        }
    }

    private void CheckGrounded()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.15f;
        isGrounded = Physics.Raycast(rayStart, Vector3.down, groundCheckDistance);
    }

    private void EvaluateAndPickTargetTile()
    {
        // Check current tile under bot
        PlatformTile currentTile = GetTileUnderfoot();

        // If standing on a safe tile that is NOT shaking, stay or move around locally
        if (currentTile != null && currentTile.IsAvailable && !currentTile.IsShaking)
        {
            // Pick a slight random offset on current or neighboring tile
            currentMoveTarget = currentTile.Position + new Vector3(Random.Range(-0.3f, 0.3f), 0, Random.Range(-0.3f, 0.3f));
            targetTile = currentTile;
            return;
        }

        // Current tile is missing or shaking! Find nearest available stable tile
        PlatformTile bestTile = FindBestAvailableTile();
        if (bestTile != null)
        {
            targetTile = bestTile;
            currentMoveTarget = bestTile.Position;
        }
        else
        {
            // Emergency panic jump in random direction
            Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
            currentMoveTarget = transform.position + randomDir * 3.0f;
        }
    }

    private PlatformTile GetTileUnderfoot()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 1.2f))
        {
            return hit.collider.GetComponent<PlatformTile>();
        }
        return null;
    }

    private PlatformTile FindBestAvailableTile()
    {
        PlatformTile[] allTiles = FindObjectsOfType<PlatformTile>();
        PlatformTile closest = null;
        float minDistance = float.MaxValue;

        foreach (PlatformTile tile in allTiles)
        {
            if (tile == null || !tile.IsAvailable || tile.IsShaking) continue;

            float dist = Vector3.Distance(transform.position, tile.Position);
            if (dist < minDistance && dist < 7.0f)
            {
                minDistance = dist;
                closest = tile;
            }
        }

        // If all surrounding tiles are shaking, fallback to any available tile
        if (closest == null)
        {
            foreach (PlatformTile tile in allTiles)
            {
                if (tile == null || !tile.IsAvailable) continue;

                float dist = Vector3.Distance(transform.position, tile.Position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closest = tile;
                }
            }
        }

        return closest;
    }

    private void CheckAutoJump(Vector3 moveDir)
    {
        if (!isGrounded || Time.time - lastJumpTime < jumpCooldown) return;

        Vector3 probeOrigin = transform.position + Vector3.up * 0.3f + moveDir * gapCheckDistance;
        bool hasGroundAhead = Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit hit, 1.5f);

        bool isGroundFalling = false;
        if (hasGroundAhead && hit.collider != null)
        {
            PlatformTile tile = hit.collider.GetComponent<PlatformTile>();
            if (tile != null && (!tile.IsAvailable || tile.IsFalling))
            {
                isGroundFalling = true;
            }
        }

        if (!hasGroundAhead || isGroundFalling)
        {
            PerformJump(moveDir);
        }
    }

    private void PerformJump(Vector3 moveDir)
    {
        lastJumpTime = Time.time;
        isGrounded = false;

        Vector3 jumpVel = rb.velocity;
        jumpVel.y = jumpForce;
        jumpVel += moveDir * forwardJumpBoost;
        rb.velocity = jumpVel;

        if (squashAndStretch != null)
        {
            squashAndStretch.TriggerJumpSquash();
        }
    }

    private void Eliminate()
    {
        isEliminated = true;
        gameObject.SetActive(false);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnBotEliminated(gameObject);
        }
    }
}
