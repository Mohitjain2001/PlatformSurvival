using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BotController : MonoBehaviour
{
    [Header("Bot Settings")]
    [SerializeField] private float moveSpeed = 6.2f;
    [SerializeField] private float rotationSpeed = 12.0f;
    [SerializeField] private float jumpForce = 8.0f;
    [SerializeField] private float forwardJumpBoost = 2.0f;
    [SerializeField] private float gapCheckDistance = 1.35f;
    [SerializeField] private float groundCheckDistance = 0.35f;
    [SerializeField] private float jumpCooldown = 0.45f;
    [SerializeField] private float minGroundedDuration = 0.25f;
    [SerializeField] private float reactionDelayMin = 0.1f;
    [SerializeField] private float reactionDelayMax = 0.35f;

    [Header("Dependencies")]
    [SerializeField] private CharacterSquashAndStretch squashAndStretch;
    [SerializeField] private Character3DAnimator characterAnimator;

    private Rigidbody rb;
    private bool isGrounded = false;
    private float groundedDuration = 0f;
    private bool isEliminated = false;
    private float lastJumpTime = -1f;
    private Vector3 currentMoveTarget;
    private PlatformTile targetTile;
    private float nextTargetEvaluationTime = 0f;
    private string botName = "Bot";
    private Material botMaterial;
    private float eliminationYThreshold = -15.0f;

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
        if (characterAnimator == null)
        {
            characterAnimator = GetComponent<Character3DAnimator>();
        }
    }

    public void Initialize(string name, Color color, float eliminationY = -15.0f)
    {
        botName = name;
        eliminationYThreshold = eliminationY;
        isEliminated = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Renderer[] allRenderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in allRenderers)
        {
            if (rend.gameObject.name.Contains("Torso") || rend.gameObject.name.Contains("Shoulder") || rend.gameObject.name.Contains("Arm"))
            {
                Material mat = new Material(rend.material);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                mat.color = color;
                rend.material = mat;
            }
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

        // Multi-layer bottom elimination check
        if (transform.position.y < eliminationYThreshold && !isEliminated)
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
            targetVelocity.y = rb.linearVelocity.y;
            rb.linearVelocity = targetVelocity;
        }
        else if (isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x * 0.7f, rb.linearVelocity.y, rb.linearVelocity.z * 0.7f);
        }
    }

    private void CheckGrounded()
    {
        Vector3 checkOrigin = transform.position + Vector3.up * 0.25f;
        float radius = 0.25f;
        RaycastHit[] hits = Physics.SphereCastAll(checkOrigin, radius, Vector3.down, groundCheckDistance);

        bool foundGround = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col != null && !col.isTrigger && col.transform != transform && !col.transform.IsChildOf(transform))
            {
                foundGround = true;
                break;
            }
        }

        isGrounded = foundGround;
        if (isGrounded)
        {
            groundedDuration += Time.deltaTime;
        }
        else
        {
            groundedDuration = 0f;
        }

        if (characterAnimator != null) characterAnimator.SetGrounded(isGrounded);
    }

    private void EvaluateAndPickTargetTile()
    {
        PlatformTile currentTile = GetTileUnderfoot();

        if (currentTile != null && currentTile.IsAvailable && !currentTile.IsShaking)
        {
            currentMoveTarget = currentTile.Position + new Vector3(Random.Range(-0.3f, 0.3f), 0, Random.Range(-0.3f, 0.3f));
            targetTile = currentTile;
            return;
        }

        PlatformTile bestTile = FindBestAvailableTileOnSameLevel();
        if (bestTile != null)
        {
            targetTile = bestTile;
            currentMoveTarget = bestTile.Position;
        }
        else
        {
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

    private PlatformTile FindBestAvailableTileOnSameLevel()
    {
        PlatformTile[] allTiles = FindObjectsOfType<PlatformTile>();
        PlatformTile closest = null;
        float minDistance = float.MaxValue;
        float botY = transform.position.y;

        foreach (PlatformTile tile in allTiles)
        {
            if (tile == null || !tile.IsAvailable || tile.IsShaking) continue;

            // Prefer tiles on current level (similar Y position)
            if (Mathf.Abs(tile.Position.y - botY) < 2.5f)
            {
                float dist = Vector3.Distance(transform.position, tile.Position);
                if (dist < minDistance && dist < 7.5f)
                {
                    minDistance = dist;
                    closest = tile;
                }
            }
        }

        // Fallback: any available tile near bot position
        if (closest == null)
        {
            foreach (PlatformTile tile in allTiles)
            {
                if (tile == null || !tile.IsAvailable) continue;

                if (tile.Position.y <= botY + 1.0f)
                {
                    float dist = Vector3.Distance(transform.position, tile.Position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closest = tile;
                    }
                }
            }
        }

        return closest;
    }

    private void CheckAutoJump(Vector3 moveDir)
    {
        if (!isGrounded || groundedDuration < minGroundedDuration || Time.time - lastJumpTime < jumpCooldown) 
            return;

        Vector3 probeOrigin = transform.position + Vector3.up * 0.35f + moveDir * gapCheckDistance;
        RaycastHit[] aheadHits = Physics.SphereCastAll(probeOrigin, 0.3f, Vector3.down, 1.8f);

        bool hasGroundAhead = false;
        bool isGroundFalling = false;

        for (int i = 0; i < aheadHits.Length; i++)
        {
            Collider col = aheadHits[i].collider;
            if (col != null && !col.isTrigger && col.transform != transform && !col.transform.IsChildOf(transform))
            {
                hasGroundAhead = true;
                PlatformTile tile = col.GetComponent<PlatformTile>();
                if (tile != null && (!tile.IsAvailable || tile.IsFalling))
                {
                    isGroundFalling = true;
                }
                break;
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
        groundedDuration = 0f;

        Vector3 jumpVel = rb.linearVelocity;
        jumpVel.y = jumpForce;
        jumpVel += moveDir * forwardJumpBoost;
        rb.linearVelocity = jumpVel;

        if (characterAnimator != null)
        {
            characterAnimator.TriggerJump();
        }

        if (squashAndStretch != null)
        {
            squashAndStretch.TriggerJumpSquash();
        }
    }

    private void Eliminate()
    {
        isEliminated = true;
        if (characterAnimator != null)
        {
            characterAnimator.SetEliminated(true);
        }
        gameObject.SetActive(false);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnBotEliminated(gameObject);
        }
    }
}
