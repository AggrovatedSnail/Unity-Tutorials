using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public float InputX, InputZ;
    public float Speed;
    public Vector3 desiredMoveDirection;
    public float desiredRotationSpeed;
    public Animator animator;
    public Camera camera;
    public bool showSolverDebug = true;
    public bool isGrounded = true;
    public CharacterController controller;
    public float Angle2Target;
    public GameObject InputDirectionCompass;
    private float verticalVelocity;
    private Vector3 moveVector;
    private bool isInAir = false;
    private bool isPistolArmed = false;
    private bool isRifleArmed = false;
    private float distanceToGround;

    public GameObject pistolOBJ;
    public GameObject rifleOBJ;
    
    Vector3 rightFootPosition;
    Vector3 leftFootPosition;
    Vector3 rightFootIKPosition;
    Vector3 leftFootIKPosition;
    Quaternion leftFootIKRotation;
    Quaternion rightFootIKRotation;
    float lastPelvisPositionY;
    float lastRightFootPositionY;
    float lastLeftFootPositionY;

    [Header("Feet Grounder")]
    public bool enableFeetIK = true;
    [Range(0,2)]
    [SerializeField]
    float heightFromGroundRaycast = 1.14f;
    [Range(0, 2)]
    [SerializeField]
    float raycastDownDistance = 1.5f;
    [SerializeField]
    LayerMask _environmentLayer;
    [SerializeField]
    float pelvisOffset;
    [Range(0, 1)]
    [SerializeField]
    float pelvisUpAndDownSpeed = 0.28f;
    [Range(0, 1)]
    [SerializeField]
    float feetToIKPositionSpeed = 0.5f;

    // Start is called before the first frame update
    void Start()
    {
        animator = this.GetComponent<Animator>();
        controller = this.GetComponent<CharacterController>();
        camera = Camera.main;
    }

    // Update is called once per frame
    void Update()
    {
        InputMagnitude();
        CheckGrounding();
    }

    void FixedUpdate()
    {
        if (!enableFeetIK)
            return;

        // Feet grounding
        AdjustFeetTarget(ref rightFootPosition, HumanBodyBones.RightFoot);
        AdjustFeetTarget(ref leftFootPosition, HumanBodyBones.LeftFoot);
        // Find and Raycast to the ground
        FeetPositionSolver(rightFootPosition, ref rightFootIKPosition, ref rightFootIKRotation);
        FeetPositionSolver(leftFootPosition, ref leftFootIKPosition, ref leftFootIKRotation);
        
        // Parent the Input Direction Compass to the Player
        InputDirectionCompass.transform.position = this.transform.position;
        InputDirectionCompass.transform.rotation = Quaternion.identity;

        InputX = Input.GetAxis("Horizontal");
        InputZ = Input.GetAxis("Vertical");
        animator.SetFloat("InputZ", InputZ, 0f, Time.deltaTime);
        animator.SetFloat("InputX", InputX, 0f, Time.deltaTime);
        Speed = new Vector2(InputX, InputZ).sqrMagnitude;
        animator.SetFloat("InputMagnitude", Speed, 0f, Time.deltaTime);

        Vector3 targetDirection = InputDirectionCompass.transform.InverseTransformPoint(desiredMoveDirection);
        Angle2Target = Mathf.Atan2(targetDirection.x, targetDirection.z) * Mathf.Rad2Deg;

        var cam = Camera.main;
        var forward = cam.transform.forward;
        var right = cam.transform.right;
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();
        desiredMoveDirection = forward * InputZ + right * InputX;
        desiredMoveDirection.y = 0f;

        InputDirectionCompass.transform.Rotate(0, 0, Angle2Target);
        InputDirectionCompass.transform.Translate(desiredMoveDirection);

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(desiredMoveDirection), desiredRotationSpeed * Time.deltaTime);

        controller.Move(desiredMoveDirection * Time.deltaTime);
    }

    void InputMagnitude() {
        bool running = Input.GetButton("Running");
        animator.SetBool("isRunning", running);
        if (running && Input.GetButtonDown("Sliding"))
            animator.SetBool("isSliding", true);
        else
            animator.SetBool("isSliding", false);

        bool crouching = Input.GetButton("Crouching");
        animator.SetBool("isCrouching", crouching);

        bool punching = Input.GetButton("Fire1");
        animator.SetBool("isPunching", punching);

        animator.SetBool("isGrounded", isGrounded);

        if (isGrounded && !isInAir)
        {
            bool jumping = Input.GetButton("Jump");
            if (jumping)
            {
                animator.SetBool("isJumping", jumping);
                isInAir = true;
                jumping = false;
            }
        }
        else
            isInAir &= !isGrounded;

        if (Input.GetButton("ArmPistol"))
        {
            isPistolArmed = !isPistolArmed;
            animator.SetBool("isPistolArmed", isPistolArmed);
            pistolOBJ.SetActive(true);
            isRifleArmed = false;
            animator.SetBool("isRifleArmed", isRifleArmed);
            rifleOBJ.SetActive(false);
        }

        if (Input.GetButton("ArmRifle"))
        {
            isRifleArmed = !isRifleArmed;
            animator.SetBool("isRifleArmed", isRifleArmed);
            isPistolArmed = false;
            animator.SetBool("isPistolArmed", isPistolArmed);
            pistolOBJ.SetActive(false);
        }

        if (Input.GetMouseButton(1))
            animator.SetBool("isAiming", true);
        else
            animator.SetBool("isAiming", false);
    }

    void CheckGrounding()
    {
        if (isGrounded)
            distanceToGround = 0.1f;
        else
            distanceToGround = 0.35f;

        if (Physics.CheckCapsule(transform.position, Vector3.down, distanceToGround, 1 << LayerMask.NameToLayer("Ground")))
            isGrounded = true;
        else
            isGrounded = false;
    }

    #region Animation Events
    public void GrabRifle()
    {
        rifleOBJ.SetActive(true);
    }

    public void PutRifleAway()
    {
        rifleOBJ.SetActive(false);
    }

    public void PutPistolAway()
    {
        pistolOBJ.SetActive(false);
    }
    #endregion
    
    #region Feet Grounding
    private void OnAnimatorIK(int layerIndex)
    {
        if (!enableFeetIK)
            return;

        MovePelvisHeight();
        animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 1);
        MoveFeetToIKPoint(AvatarIKGoal.RightFoot, rightFootIKPosition, rightFootIKRotation, ref lastRightFootPositionY);
        animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1);
        MoveFeetToIKPoint(AvatarIKGoal.LeftFoot, leftFootIKPosition, leftFootIKRotation, ref lastLeftFootPositionY);
    }
    #endregion

    #region Feet Grounding Methods
    void MoveFeetToIKPoint(AvatarIKGoal foot, Vector3 positionIKHolder, Quaternion rotationIKHolder, ref float lastFootPositionY)
    {
        Vector3 targetIKPosition = _animator.GetIKPosition(foot);
        if (positionIKHolder != Vector3.zero)
        {
            targetIKPosition = transform.InverseTransformPoint(targetIKPosition);
            positionIKHolder = transform.InverseTransformPoint(positionIKHolder);
            float y = Mathf.Lerp(lastFootPositionY, positionIKHolder.y, _feetToIKPositionSpeed);
            targetIKPosition.y += y;
            lastFootPositionY = y;
            targetIKPosition = transform.TransformPoint(targetIKPosition);
            _animator.SetIKRotation(foot, rotationIKHolder);
        }

        _animator.SetIKPosition(foot, targetIKPosition);
    }

    void MovePelvisHeight()
    {
        if (rightFootIKPosition == Vector3.zero || leftFootIKPosition == Vector3.zero ||
            lastPelvisPositionY == 0)
        {
            lastPelvisPositionY = animator.bodyPosition.y;
            return;
        }

        float leftOffsetPosition = leftFootIKPosition.y - transform.position.y;
        float rightOffsetPosition = rightFootIKPosition.y - transform.position.y;
        float totalOffset = (leftOffsetPosition < rightOffsetPosition) ? leftOffsetPosition : rightOffsetPosition;
        Vector3 newPelvisPosition = animator.bodyPosition + Vector3.up * totalOffset;
        newPelvisPosition.y = Mathf.Lerp(lastPelvisPositionY, newPelvisPosition.y, pelvisUpAndDownSpeed);
        animator.bodyPosition = newPelvisPosition;
        lastPelvisPositionY = animator.bodyPosition.y;
    }

    void FeetPositionSolver(Vector3 fromSkyPosition, ref Vector3 feetIKPositions, ref Quaternion feetIKRotations)
    {
        // Raycast handling section
        RaycastHit feetOutHit;
        if (showSolverDebug)
            Debug.DrawLine(fromSkyPosition, fromSkyPosition + Vector3.down * (raycastDownDistance + heightFromGroundRaycast), Color.yellow);

        if (Physics.Raycast(fromSkyPosition, Vector3.down, out feetOutHit, raycastDownDistance + heightFromGroundRaycast, environmentLayer))
        {
            feetIKPositions = fromSkyPosition;
            feetIKPositions.y = feetOutHit.point.y + pelvisOffset;
            feetIKRotations = Quaternion.FromToRotation(Vector3.up, feetOutHit.normal) * transform.rotation;
            return;
        }

        feetIKPositions = Vector3.zero;
    }

    void AdjustFeetTarget(ref Vector3 feetPositions, HumanBodyBones foot)
    {
        feetPositions = animator.GetBoneTransform(foot).position;
        feetPositions.y = transform.position.y + _heightFromGroundRaycast;
    }
    #endregion
}
