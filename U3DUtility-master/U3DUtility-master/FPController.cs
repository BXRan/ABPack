using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using Random = UnityEngine.Random;

namespace U3DUtility
{
    public delegate void FPCallbackFuntion();

    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(AudioSource))]
    public class FPController : MonoBehaviour
    {
        [SerializeField] float m_WalkSpeed = 3.5f;
        [SerializeField] float m_RunSpeed = 4.5f;
        [SerializeField] [Range(0f, 1f)] float m_RunStepLength = 0f;
        [SerializeField] float m_JumpSpeed = 4.2f;
        [SerializeField] float m_StickToGroundForce = 0f;
        [SerializeField] float m_GravityMultiplier = 1f;
        [SerializeField] float m_StepInterval = 0f;
        [SerializeField] AudioClip[] m_FootstepSounds = null;
        [SerializeField] AudioClip m_JumpSound = null;
        [SerializeField] AudioClip m_LandSound = null;
        [SerializeField] float m_CheckJumpDistance = 0.5f;
        [SerializeField] public bool m_UseHeadBob = false;
        [SerializeField] CurveControlledBob m_HeadBob = new CurveControlledBob();
        [SerializeField] LerpControlledBob m_JumpBob = new LerpControlledBob();
        [SerializeField] bool m_UseFovKick = false;
        [SerializeField] FOVKick m_FovKick = new FOVKick();
        [SerializeField] FPLook m_Look = new FPLook();

        public FPCallbackFuntion OnMovingFunc;
        public FPCallbackFuntion OnDropFunc;
        public FPCallbackFuntion OnStopMoveFunc;

        private Vector2 m_Move;
        private bool m_Jump;
        private bool m_Jumping;
        private bool m_IsWalking;
        private bool m_IsWaterTop;
        private float m_NextStep;
        private float m_StepCycle;
        private Vector3 m_MoveDir;
        private Vector3 m_OriginalCameraPosition;
        private CharacterController m_CharacterController = null;
        private Camera m_Camera;
        private AudioSource m_AudioSource;
        private bool m_PreviouslyGrounded;
        private bool m_IsEnableJump = true;
        private float m_DropSpeed;

        public bool Jump
        {
            set { m_Jump = value; }
        }

        public bool IsEnableLook
        {
            set { m_Look.IsEnable = value; }
        }

        public bool IsJumping
        {
            get { return m_Jumping; }
        }

        public bool IsEnableJump
        {
            set { m_IsEnableJump = value; }
        }

        public bool IsWaterTop
        {
            set { m_IsWaterTop = value; }
        }

        public bool IsWalk
        {
            set
            {
                // handle speed change to give an fov kick
                // only if the player is going to a run, is running and the fovkick is to be used
                if (m_IsWalking != value && m_UseFovKick && m_CharacterController.velocity.sqrMagnitude > 0)
                {
                    StopAllCoroutines();
                    StartCoroutine(!m_IsWalking ? m_FovKick.FOVKickUp() : m_FovKick.FOVKickDown());
                }

                m_IsWalking = value;
            }
        }

        public float GravityMultiplier
        {
            get { return m_GravityMultiplier; }
            set { m_GravityMultiplier = value; }
        }

        public AudioClip JumpSound
        {
            set { m_JumpSound = value; }
        }

        public float DropSpeed
        {
            get { return m_DropSpeed; }
        }

        public void ResetMoveDir()
        {
            m_MoveDir = Vector3.zero;
        }

        public void SetMove (Vector2 move)
        {
            m_Move = move;
        }

        public void SetMoveSpeed (float runSpeed, float walkSpeed)
        {
            m_RunSpeed = runSpeed;
            m_WalkSpeed = walkSpeed;
        }

        public void RotateView(Vector2 delta)
        {
            m_Look.LookRotation(transform, m_Camera.transform, delta.y, delta.x);
        }

        public void SetStepSounds(AudioClip[] sounds)
        {
            m_FootstepSounds = sounds;
        }

        public void InitCamera (Camera camera)
        {
            if (camera == null)
            {
                Debug.LogError("camera == null");
                return;
            }

            m_Camera = camera;
            m_OriginalCameraPosition = m_Camera.transform.localPosition;
            m_Look.Init(transform, m_Camera.transform);
        }

        private void Start ()
        {
            m_CharacterController = GetComponent<CharacterController>();
            m_AudioSource = GetComponent<AudioSource>();
            m_StepCycle = 0f;
            m_NextStep = m_StepCycle / 2f;
            m_Jumping = false;
        }

        private void Update ()
        {
            if (m_CharacterController.isGrounded)
            {
                if (m_MoveDir.y < -100 && OnDropFunc != null)
                {
                    m_DropSpeed = (int)m_MoveDir.y;
                    OnDropFunc();
                    m_DropSpeed = 0;
                }
            }

            if (!m_PreviouslyGrounded && m_CharacterController.isGrounded)
            {
                StartCoroutine(m_JumpBob.DoBobCycle());
                PlayLandingSound();

                m_MoveDir.y = 0f;
                m_Jumping = false;

            }

            if (!m_CharacterController.isGrounded && !m_Jumping && m_PreviouslyGrounded)
            {
                m_MoveDir.y = 0f;
            }

            m_PreviouslyGrounded = m_CharacterController.isGrounded;
        }

        void FixedUpdate()
        {
            ProcessMove();
        }

        void PlayLandingSound()
        {
            if (m_LandSound == null)
                return;
            m_AudioSource.clip = m_LandSound;
            m_AudioSource.Play();
            m_NextStep = m_StepCycle + .5f;
        }

        void PlayJumpSound()
        {
            m_AudioSource.clip = m_JumpSound;
            m_AudioSource.Play();
        }

        void ProgressStepCycle(float speed)
        {
            if (m_CharacterController.velocity.sqrMagnitude > 0 && (m_Move.x != 0 || m_Move.y != 0))
            {
                m_StepCycle += (m_CharacterController.velocity.magnitude + (speed * (m_IsWalking ? 1f : m_RunStepLength))) *
                             Time.fixedDeltaTime;
            }

            if (!(m_StepCycle > m_NextStep))
            {
                return;
            }

            m_NextStep = m_StepCycle + m_StepInterval;

            PlayFootStepAudio();
        }

        void PlayFootStepAudio()
        {
            if (OnMovingFunc != null)
                OnMovingFunc();

            if (!m_CharacterController.isGrounded)
                return;

            if (m_AudioSource.isPlaying)
                return;

            if (m_FootstepSounds == null || m_FootstepSounds.Length == 0)
                return;

            int n = Random.Range(0, m_FootstepSounds.Length);
            m_AudioSource.clip = m_FootstepSounds[n];
            m_AudioSource.Play();
        }

        void ProcessMove()
        {
            float speed = m_IsWalking ? m_WalkSpeed : m_RunSpeed; ;

            // always move along the camera forward as it is the direction that it being aimed at
            Vector3 oldDesiredMove = transform.forward * m_Move.y + transform.right * m_Move.x;
            oldDesiredMove.Normalize();

            // get a normal for the surface that is being touched to move along it
            int delLayerIndex = 1 << LayerMask.NameToLayer("地图_花草");
            int layerIndex = ~delLayerIndex;

            RaycastHit hitInfo;
            bool hitFront = Physics.SphereCast(transform.position, m_CharacterController.radius, oldDesiredMove, out hitInfo,
                m_CheckJumpDistance, layerIndex, QueryTriggerInteraction.Ignore);//1 << 9

            Vector3 desiredMove = Vector3.ProjectOnPlane(oldDesiredMove, hitInfo.normal).normalized;
            m_MoveDir.x = desiredMove.x * speed;
            m_MoveDir.z = desiredMove.z * speed;

            if (m_CharacterController.isGrounded || m_IsWaterTop)
            {
                m_MoveDir.y = -m_StickToGroundForce;

                if (m_MoveDir.magnitude > 0.1f) //有移动量操作才可以自动跳，没移动操作就原地
                {
                    if (hitFront) //检查是否可以跳过阻挡
                    {
                        Vector3 checkMoveDir = oldDesiredMove;
                        checkMoveDir.Normalize();

                        bool hitup = Physics.SphereCast(transform.position + new Vector3(0, m_CharacterController.height / 4, 0),
                                                        m_CharacterController.radius, checkMoveDir, out hitInfo,
                                                        m_CheckJumpDistance, layerIndex, QueryTriggerInteraction.Ignore);

                        if (!hitup) //移动方向上能跳就跳
                        {
                            m_Jump = true;
                            m_MoveDir.x = oldDesiredMove.x * speed;
                            m_MoveDir.z = oldDesiredMove.z * speed;
                        }
                        else //检测滑动方向上有阻挡并可以跳也跳
                        {
                            checkMoveDir = Vector3.ProjectOnPlane(checkMoveDir, hitInfo.normal).normalized;

                            hitFront = Physics.SphereCast(transform.position,
                                                        m_CharacterController.radius, checkMoveDir, out hitInfo,
                                                        m_CheckJumpDistance, layerIndex, QueryTriggerInteraction.Ignore);

                            hitup = Physics.SphereCast(transform.position + new Vector3(0, m_CharacterController.height / 4, 0),
                                                        m_CharacterController.radius, checkMoveDir, out hitInfo,
                                                        m_CheckJumpDistance, layerIndex, QueryTriggerInteraction.Ignore);

                            if (!hitup && hitFront)
                            {
                                m_Jump = true;
                                m_MoveDir.x = oldDesiredMove.x * speed;
                                m_MoveDir.z = oldDesiredMove.z * speed;
                            }
                        }
                    }
                }

                if (m_Jump)
                {
                    m_MoveDir.y = m_JumpSpeed * 1.6f;//1.6起跳速度
                    PlayJumpSound();
                    m_Jump = false;
                    m_Jumping = true;
                    m_IsWaterTop = false;
                }

            }
            else
            {
                m_MoveDir += Physics.gravity * 3 * m_GravityMultiplier * Time.fixedDeltaTime;//3掉落速度
            }

            m_CharacterController.Move(m_MoveDir * Time.fixedDeltaTime);

            ProgressStepCycle(speed);
            UpdateCameraPosition(speed);

            m_Look.UpdateCursorLock();
        }

        void UpdateCameraPosition(float speed)
        {
            Vector3 newCameraPosition;
            if (!m_UseHeadBob)
            {
                return;
            }

            if (m_CharacterController.velocity.magnitude > 0 && m_CharacterController.isGrounded)
            {
                m_Camera.transform.localPosition =
                    m_HeadBob.DoHeadBob(m_CharacterController.velocity.magnitude +
                                      (speed * (m_IsWalking ? 1f : m_RunStepLength)));
                newCameraPosition = m_Camera.transform.localPosition;
                newCameraPosition.y = m_Camera.transform.localPosition.y - m_JumpBob.Offset();
            }
            else
            {
                newCameraPosition = m_Camera.transform.localPosition;
                newCameraPosition.y = m_OriginalCameraPosition.y - m_JumpBob.Offset();
            }
            m_Camera.transform.localPosition = newCameraPosition;
        }

    }
}
