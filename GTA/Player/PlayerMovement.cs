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
    public CharacterController controller;
    public float Angle2Target;
    public GameObject InputDirectionCompass;
    private float verticalVelocity;
    private Vector3 moveVector;

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
        controller.Move(desiredMoveDirection);
    }

    void InputMagnitude() {
        // Parent the Input Direction Compass to the Player
        InputDirectionCompass.transform.position = this.transform.position;
        InputDirectionCompass.transform.rotation = Quaternion.identity;

        InputX = Input.GetAxis("Horizontal");
        InputZ = Input.GetAxis("Vertical");
        animator.SetFloat("InputZ", InputZ, 0f, Time.deltaTime);
        animator.SetFloat("InputX", InputX, 0f, Time.deltaTime);
        Speed = new Vector2(InputX, InputZ).sqrMagnitude;
        animator.SetFloat("InputMagnitude", Speed, 0f, Time.deltaTime);

        bool running = Input.GetButton("Running");
        animator.SetBool("isRunning", running);
        if (running && Input.GetButtonDown("Sliding"))
            animator.SetBool("isSliding", true);
        else
            animator.SetBool("isSliding", false);

        bool crouching = Input.GetButton("Crouching");
        animator.SetBool("isCrouching", crouching);

        bool jumping = Input.GetButton("Jump");
        if (jumping)
            animator.SetBool("isJumping", jumping);
        else
            animator.SetBool("isJumping", false);

        Vector3 targetDirection = InputDirectionCompass.transform.InverseTransformPoint(desiredMoveDirection);
        Angle2Target = Mathf.Atan2(targetDirection.x, targetDirection.z) * Mathf.Rad2Deg;

        InputDirectionCompass.transform.Rotate(0, 0, Angle2Target);
        InputDirectionCompass.transform.Translate(desiredMoveDirection);

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(desiredMoveDirection), desiredRotationSpeed * Time.deltaTime);

        var cam = Camera.main;
        var forward = cam.transform.forward;
        var right = cam.transform.right;
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();
        desiredMoveDirection = (forward * InputZ * Time.deltaTime) + (right * InputX * Time.deltaTime);
    }
}
