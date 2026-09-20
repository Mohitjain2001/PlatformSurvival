using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BotController : MonoBehaviour
{
    [Header("Bot Settings")]
    [SerializeField] private float moveSpeed = 2.8f; // Slow speed (Player is 5.2f) so player easily outruns them!
    [SerializeField] private float acceleration = 7.0f;
    [SerializeField] private float deceleration = 8.0f;
    [SerializeField] private float rotationSpeed = 8.0f;
    [SerializeField] private float jumpForce = 5.2f; // Weak jump so bots often fall into gaps
    [SerializeField] private float forwardJumpBoost = 0.8f;
    [SerializeField] private float gapCheckDistance = 0.90f;
    [SerializeField] private float groundCheckDistance = 0.35f;
    [SerializeField] private float jumpCooldown = 0.75f;
    [SerializeField] private float minGroundedDuration = 0.35f;
    [SerializeField] private float reactionDelayMin = 0.50f; // Slow reaction (500ms - 1100ms)
    [SerializeField] private float reactionDelayMax = 1.10f;
    [SerializeField] private float mistakeChance = 0.50f; // 50% chance of clumsy mistake

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

        // Add white floating 3D name tag above bot's head
        CharacterNameTag nameTag = GetComponent<CharacterNameTag>();
        if (nameTag == null) nameTag = gameObject.AddComponent<CharacterNameTag>();
        nameTag.Setup(botName, Color.white, 1.85f);
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
        Vector3 currentHorizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

        if (moveDir.sqrMagnitude > 0.05f)
        {
            moveDir.Normalize();

            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);

            Vector3 targetVelocity = moveDir * moveSpeed;
            Vector3 smoothedVel = Vector3.Lerp(currentHorizontalVel, targetVelocity, acceleration * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector3(smoothedVel.x, rb.linearVelocity.y, smoothedVel.z);
        }
        else if (isGrounded)
        {
            Vector3 smoothedStopVel = Vector3.Lerp(currentHorizontalVel, Vector3.zero, deceleration * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector3(smoothedStopVel.x, rb.linearVelocity.y, smoothedStopVel.z);
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
        bool makeMistake = Random.value < mistakeChance;

        // If standing on a valid tile, stay on it unless shaking (or if making a mistake, delay leaving)
        if (currentTile != null && currentTile.IsAvailable && (!currentTile.IsShaking || makeMistake))
        {
            if (!makeMistake)
            {
                currentMoveTarget = currentTile.Position + new Vector3(Random.Range(-0.4f, 0.4f), 0, Random.Range(-0.4f, 0.4f));
                targetTile = currentTile;
                return;
            }
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
        IList<PlatformTile> allTiles = null;
        if (PlatformGridGenerator.Instance != null)
        {
            allTiles = PlatformGridGenerator.Instance.ActiveTiles;
        }
        else
        {
            allTiles = FindObjectsOfType<PlatformTile>();
        }

        PlatformTile closest = null;
        float minDistance = float.MaxValue;
        float botY = transform.position.y;

        for (int i = 0; i < allTiles.Count; i++)
        {
            PlatformTile tile = allTiles[i];
            if (tile == null || !tile.IsAvailable) continue;
            
            // 50% chance of clumsy mistake: bot ignores shaking warning!
            if (tile.IsShaking && Random.value > mistakeChance) continue;

            // Only look at immediate nearby tiles (radius 3.2m instead of full map radar)
            if (Mathf.Abs(tile.Position.y - botY) < 2.5f)
            {
                float dist = Vector3.Distance(transform.position, tile.Position);
                if (dist < minDistance && dist < 3.2f)
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
        if (!isGrounded || groundedDuration < minGroundedDuration || Time.time - lastJumpTime < jumpCooldown) 
            return;

        // 25% chance of stumbling / missing jump timing!
        if (Random.value < 0.25f) return;

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
