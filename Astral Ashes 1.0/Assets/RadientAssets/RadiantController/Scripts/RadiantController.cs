using UnityEngine;
using UnityEngine.InputSystem;



namespace RadiantAssets{


[RequireComponent(typeof(CharacterController))]

[RequireComponent(typeof(PlayerInput))]


public class RadiantController : MonoBehaviour{
   public float WalkSpeed = 2.0f;
   public float RunSpeed = 5.5f;
   [Range(0.0f,0.3f)] public float RotationSmoothTime = 0.12f;
   public float SpeedChangeRate = 10.0f;

   [Space(2)]
   public float JumpHeight = 1.2f;
   public float Gravity = -15.0f;

   [Space(2)]
   public float JumpTimeout = 0.50f;
   public float FallTimeout = 0.15f;

   [Space(2)]
   public bool Grounded = true;
   public float GroundedOffset = -0.14f;
   public float GroundedRadius = 0.28f;
   public LayerMask GroundLayers; 


   [Space(2)]
   public GameObject CinemachineCameraTarget;
   public float UchuClamp = 70.0f;
   public float NichuClamp = -30.0f;
   public float CameraAngleOverride = 0.0f;
   public bool LockCameraPosition = false; //locking camera position on all axis

   [Space(2)]
   public float _cinemachineTargetYaw;
   public float _cinemachineTargetPitch;

    //radiant values
    private float _speed;
    private float _animationBlend;
    private float _targetRotation = 0.0f;
    private float _rotationVelocity;
    private float _verticalVelocity;
    private float _terminalVelocity = 53.0f;

    //timeout deltaTime
    private float _jumpTimeoutDelta;
    private float _fallTimeoutDelta;

    //animation IDs
    private int _aIDSpeed;
    private int _aIDGrounded;
    private int _aIDJump;
    private int _aIDFreeFall;
    private int _aIDMotionSpeed;

    private Animator _animator;
    private CharacterController _controller;
    private RadiantInput _input; 
    private GameObject _mainCamera;


    private const float _threshold = 0.01f;

    private bool _hasAnimator;

    private void Awake(){


         // get a referance to main camera 
         if (_mainCamera == null){
             _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
         }


    }

    private void Start(){   


         // collect animator, player, inputs 
         _hasAnimator = TryGetComponent(out _animator);
         _controller = GetComponent<CharacterController>();
         _input = GetComponent<RadiantInput>();

         AssignAnimationIDs();

         //reseting timeouts on start
         _jumpTimeoutDelta = JumpTimeout;
         _fallTimeoutDelta = FallTimeout;

    
    
    }


    

    private void Update(){


         // update game by animator commands 
         _hasAnimator = TryGetComponent(out _animator);


         // do actions from commands
         JumpAndGravity();
         GroundedCheck();
         Move();

    }


    private void LateUpdate(){

        CameraRotation();

    }


    private void AssignAnimationIDs(){

        _aIDSpeed = Animator.StringToHash("Speed");
        _aIDGrounded = Animator.StringToHash("Grounded");
        _aIDJump = Animator.StringToHash("Jump");
        _aIDFreeFall = Animator.StringToHash("FreeFall");
        _aIDMotionSpeed = Animator.StringToHash("MotionSpeed");

    }


    private void GroundedCheck(){


        //Set sphere position, with offset - radiantfo sho
        Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
        Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);

        //update animator if character on ground
        if (_hasAnimator){
            
            _animator.SetBool(_aIDGrounded, Grounded);
            
        }

    }


    private void CameraRotation(){


        //if there's an input and camera location is not fixed 
        if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition){
            
            _cinemachineTargetYaw += _input.look.x * Time.deltaTime;
            _cinemachineTargetPitch += _input.look.y * Time.deltaTime;
            
        }


        //clamping nichu uchu rotation so that values are limited to 360 degrees
        _cinemachineTargetYaw = ClampAngle (_cinemachineTargetYaw, float.MinValue, float.MaxValue);
        _cinemachineTargetPitch = ClampAngle (_cinemachineTargetPitch, NichuClamp, UchuClamp);


        //cinemachine will follow this target 
        CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride, _cinemachineTargetYaw, 0.0f);
       

    }


    private void Move(){

        // set target speed based on WalkSpeed, and change to run speed if sprint is pressed
        float targetSpeed = _input.sprint ? RunSpeed : WalkSpeed;

        // if no input target speed = 0
        if (_input.move == Vector2.zero) targetSpeed = 0.0f;

        //current horizontal velocity
        float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

        float speedOffset = 0.1f;
        float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f; 


        if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset ){
            
            //create a curve result rather than a linear one creating more organic speed change
            // note T in Lerp is clamped so we dont need to clamp our speed
            _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);

            //round speed needs 3 decimal places 
            _speed = Mathf.Round(_speed * 1000f) / 1000f;
            
        }
        else {

            _speed = targetSpeed;

        }

        _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate );

        //normalize input direction
        Vector3 inputDirection = new Vector3(_input.move.x , 0.0f , _input.move.y).normalized;

        //note: Vector2's != operator uses approximation so it's not floating point error prone, and also cheaper than magnitude
        //if there's a move input it rotaes player when player moving 
        if (_input.move != Vector2.zero){

            _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + _mainCamera.transform.eulerAngles.y;
            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, RotationSmoothTime);

            //rotate to face input direction relative to camera position
            transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);

        }

        Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

        //move the player
        _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
        


        //update animator if using character
        if (_hasAnimator){

            _animator.SetFloat(_aIDSpeed, _animationBlend);
            _animator.SetFloat(_aIDMotionSpeed, inputMagnitude);


        }

    }

    private void JumpAndGravity(){
        
        if (Grounded){

            //reset the fallTimeout timer
            _fallTimeoutDelta = FallTimeout;


            //update animator if using character
            if (_hasAnimator){
                _animator.SetBool(_aIDJump, false );
                _animator.SetBool(_aIDFreeFall, false);
            }


            //Stop velocity dropping infinitly when grounded
            if (_verticalVelocity < 0.0f){

                _verticalVelocity = -2f;

            }


            // Jump
            if(_input.jump && _jumpTimeoutDelta <= 0.0f){

                // the square root of H * -2 * G = How much velocity needed to reach required height
                _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);


                //update animator using character with determined vertical velocity
                if (_hasAnimator){

                    _animator.SetBool(_aIDJump, true);
                    

                }
                
            }
            // jump timeout

            if (_jumpTimeoutDelta >= 0.0f){


                _jumpTimeoutDelta -= Time.deltaTime;
                
            }



        }
        else {

            //reset the fallTimeout timer
            _jumpTimeoutDelta = JumpTimeout;



            //fall timeout
            if(_fallTimeoutDelta >= 0.0f){

                _fallTimeoutDelta -= Time.deltaTime;

            }
            else{

                if (_hasAnimator){

                    _animator.SetBool(_aIDFreeFall, true);

                }


                //if we are not grounded do not jump
                _input.jump = false;

            }

            //increase gravity overtime if vertical speed is less than terminal speed (multiple by delta time twice to linearly speed up overtime)
            if (_verticalVelocity < _terminalVelocity){

                _verticalVelocity += Gravity * Time.deltaTime;

            }




        }







    }


    private static float ClampAngle(float lfAngle, float lfMin, float lfMax){


        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle >  360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }






}


}
