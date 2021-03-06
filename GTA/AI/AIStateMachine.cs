using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Public enums of the AI System
public enum AIStateType { None, Idle, Walk, Alerted, Patrol, Pursuit, Dead }
public enum AITargetType { None, Waypoint, Visual_Player, Audio }
public enum AITriggerEventType { Enter, Stay, Exit }

// Base class for all AI State Machines
public abstract class AIStateMachine : MonoBehaviour
{
    public AITarget VisualThreat = new AITarget();
    public AITarget AudioThreat = new AITarget();

    protected AIState _currentState;
    protected Dictionary<AIStateType, AIState> _states = new Dictionary<AIStateType, AIState>();
    protected AITarget _target = new AITarget();
    protected int _rootPositionRefCount;
    protected int _rootRotationRefCount;
    protected bool _isTargetReached;

    [SerializeField]
    protected AIStateType _currentStateType = AIStateType.Idle;
    [SerializeField]
    protected SphereCollider _targetTrigger;
    [SerializeField]
    protected SphereCollider _sensorTrigger;
    [SerializeField]
    protected Path _waypointNetwork;
    [SerializeField]
    protected bool _randomPatrol = false;
    [SerializeField]
    protected int _currentWaypoint = -1;
    [SerializeField]
    [Range(0, 15)]
    protected float _stoppingDistance = 1f;

    protected Animator _animator;
    protected NavMeshAgent _agent;
    protected Collider _collider;
    protected Transform _transform;

    public bool isTargetReached { get { return _isTargetReached; } }
    public Animator animator { get { return _animator; } }
    public NavMeshAgent agent { get { return _agent; } }
    public Vector3 sensorPosition
    {
        get
        {
            if (_sensorTrigger == null)
                return Vector3.zero;
            Vector3 point = _sensorTrigger.transform.position;
            point.x += _sensorTrigger.center.x * _sensorTrigger.transform.lossyScale.x;
            point.y += _sensorTrigger.center.y * _sensorTrigger.transform.lossyScale.y;
            point.z += _sensorTrigger.center.z * _sensorTrigger.transform.lossyScale.z;
            return point;
        }
    }

    public float sensorRadius
    {
        get
        {
            if (_sensorTrigger == null)
                return 0f;

            float radius = Mathf.Max(_sensorTrigger.radius * _sensorTrigger.transform.lossyScale.x, _sensorTrigger.radius * _sensorTrigger.transform.lossyScale.y);
            return Mathf.Max(radius, _sensorTrigger.radius * _sensorTrigger.transform.lossyScale.z);
        }
    }

    public bool useRootPosition { get { return _rootPositionRefCount > 0; } }
    public bool useRootRotation { get { return _rootRotationRefCount > 0; } }
    public AITargetType targetType {  get { return _target.type; } }
    public Vector3 targetPosition { get { return _target.position; } }
    public int targetColliderID
    {
        get
        {
            if (_target.collider)
                return _target.collider.GetInstanceID();
            else
                return -1;
        }
    }

    public virtual void Awake()
    {
        _transform = transform;
        _animator = GetComponent<Animator>();
        _agent = GetComponent<NavMeshAgent>();
        _collider = GetComponent<Collider>();

        if (GameSceneManager.instance != null)
        {
            if (_collider)
                GameSceneManager.instance.RegisterAIStateMachine(_collider.GetInstanceID(), this);
            if (_sensorTrigger)
                GameSceneManager.instance.RegisterAIStateMachine(_sensorTrigger.GetInstanceID(), this);
        }
    }

    protected virtual void Start()
    {
        if (_sensorTrigger != null)
        {
            AISensor script = _sensorTrigger.GetComponent<AISensor>();
            if (script != null)
                script.ParentStateMachine = this;
        }

        AIState[] states = GetComponents<AIState>();

        foreach (AIState state in states)
        {
            if (state != null && !_states.ContainsKey(state.GetStateType()))
            {
                _states[state.GetStateType()] = state;
                state.SetStateMachine(this);
            }
        }

        if (_states.ContainsKey(_currentStateType))
        {
            _currentState = _states[_currentStateType];
            _currentState.OnEnterState();
        }
        else
            _currentState = null;

        if (_animator)
        {
            AIStateMachineLink[] scripts = _animator.GetBehaviours<AIStateMachineLink>();
            foreach (AIStateMachineLink script in scripts)
            {
                script.StateMachine = this;
            }
        }
    }

    public Vector3 GetWaypointPosition(bool increment)
    {
        if (_currentWaypoint == -1)
        {
            if (_randomPatrol)
                _currentWaypoint = Random.Range(0, _waypointNetwork.nodes.Count);
            else
                _currentWaypoint = 0;
        }
        else if (increment)
            NextWaypoint();

        if (_waypointNetwork.nodes[_currentWaypoint] != null)
        {
            Transform newWaypoint = _waypointNetwork.nodes[_currentWaypoint];
            SetTarget(AITargetType.Waypoint, null, newWaypoint.position, Vector3.Distance(newWaypoint.position, transform.position));
            return newWaypoint.position;
        }

        return Vector3.zero;
    }

    private void NextWaypoint()
    {
        if (_randomPatrol && _waypointNetwork.nodes.Count > 1)
        {
            int oldWaypoint = _currentWaypoint;
            while (_currentWaypoint == oldWaypoint)
            {
                _currentWaypoint = Random.Range(0, _waypointNetwork.nodes.Count);
            }
        }
        else
            _currentWaypoint = _currentWaypoint == _waypointNetwork.nodes.Count - 1 ? 0 : _currentWaypoint + 1;
    }

    protected virtual void FixedUpdate()
    {
        VisualThreat.Clear();
        AudioThreat.Clear();

        if (_target.type != AITargetType.None)
        {
            _target.distance = Vector3.Distance(_transform.position, _target.position);
        }

        _isTargetReached = false;
    }

    protected virtual void Update()
    {
        if (_currentState == null)
            return;

        AIStateType newStateType = _currentState.OnUpdate();
        if (newStateType != _currentStateType)
        {
            AIState newState;
            if (_states.TryGetValue(newStateType, out newState))
            {
                _currentState.OnExitState();
                newState.OnEnterState();
                _currentState = newState;
            }
            else if (_states.TryGetValue(AIStateType.Idle, out newState))
            {
                _currentState.OnExitState();
                newState.OnEnterState();
                _currentState = newState;
            }

            _currentStateType = newStateType;
        }
    }

    public virtual void OnTriggerEvent(AITriggerEventType type, Collider other)
    {
        if (_currentState != null)
            _currentState.OnTriggerEvent(type, other);
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (_targetTrigger == null || other != _targetTrigger)
            return;

        _isTargetReached = true;

        if (_currentState)
            _currentState.OnDestinationReached(true);
    }

    protected virtual void OnTriggerStay(Collider other)
    {
        if (_targetTrigger == null || other != _targetTrigger)
            return;

        _isTargetReached = true;
    }

    protected void OnTriggerExit(Collider other)
    {
        if (_targetTrigger == null || _targetTrigger != other)
            return;

        _isTargetReached = false;

        if (_currentState != null)
            _currentState.OnDestinationReached(false);
    }

    public void SetTarget(AITargetType t, Collider c, Vector3 p, float d)
    {
        _target.Set(t, c, p, d);
        if (_targetTrigger != null)
        {
            _targetTrigger.radius = _stoppingDistance;
            _targetTrigger.transform.position = _target.position;
            _targetTrigger.enabled = true;
        }
    }

    public void SetTarget(AITargetType t, Collider c, Vector3 p, float d, float sd)
    {
        _target.Set(t, c, p, d);
        if (_targetTrigger != null)
        {
            _targetTrigger.radius = sd;
            _targetTrigger.transform.position = _target.position;
            _targetTrigger.enabled = true;
        }
    }

    public void SetTarget(AITarget t)
    {
        _target = t;
        if (_targetTrigger != null)
        {
            _targetTrigger.radius = _stoppingDistance;
            _targetTrigger.transform.position = t.position;
            _targetTrigger.enabled = true;
        }
    }

    public void ClearTarget()
    {
        _target.Clear();
        if (_targetTrigger != null)
            _targetTrigger.enabled = false;
    }

    protected virtual void OnAnimatorMove()
    {
        if (_currentState != null)
            _currentState.OnAnimatorUpdated();
    }

    protected virtual void OnAnimatorIK()
    {
        if (_currentState != null)
            _currentState.OnAnimatorIKUpdated();
    }

    public void NavAgentControl(bool positionUpdate, bool rotationUpdate)
    {
        if (_agent)
        {
            _agent.updatePosition = positionUpdate;
            _agent.updateRotation = rotationUpdate;
        }
    }

    public void AddRootMotionRequest(int rootPosition, int rootRotation)
    {
        _rootPositionRefCount += rootPosition;
        _rootRotationRefCount += rootRotation;
    }
}
