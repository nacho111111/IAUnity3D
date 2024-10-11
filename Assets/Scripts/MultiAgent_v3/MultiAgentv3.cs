using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEngine.UIElements;
using System;
using Unity.MLAgents.Sensors;
using static UnityEngine.GraphicsBuffer;

public class MultiAgentv3 : Agent
{
    [HideInInspector] public Color colorState;
    public float Energy; // energia restante
    [HideInInspector] public float MaxEnergy;
    [HideInInspector] public float InitialEnergy;
    private float m_InitialEnergy;
    [HideInInspector] public float LossEnergy;
    [HideInInspector] public float MovSpeed;
    [HideInInspector] public int reproductionRate;
    public int m_FoodCount = 0;

    private float m_RealLossEnergy; // este varia entre 0 y LossOEnergy
    [HideInInspector] public Team team;

    private float m_Existential; // recompensa por tiempo de ejecucion 

    // state
    public bool state;  // estado, si ha comido o no
    [HideInInspector] public float IntervalTimerState;
    // end state

    // references 
    private Renderer m_Renderer;
    [HideInInspector] public Rigidbody agentRb;
    private EnvControllerv3 m_envController;
    BehaviorParameters m_BehaviorParameters;
    //end references
    private TimerManager m_TimeManager;

    public override void Initialize()
    {
        agentRb = GetComponent<Rigidbody>();
        m_Renderer = GetComponentInParent<Renderer>();
        m_envController = gameObject.GetComponentInParent<EnvControllerv3>();
        m_TimeManager = GetComponentInParent<TimerManager>();

        m_Existential = 1f / m_envController.MaxEnvironmentSteps;
        m_RealLossEnergy = LossEnergy;
        m_InitialEnergy = InitialEnergy * MaxEnergy;

        m_BehaviorParameters = gameObject.GetComponent<BehaviorParameters>();

        if (m_BehaviorParameters.TeamId == (int)Team.Hunted)
        {
            team = Team.Hunted;
        }
        else
        {
            team = Team.Hunter;
        }
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(state); // bool, 1 observacion
        sensor.AddObservation(transform.InverseTransformDirection(agentRb.velocity)); // referencia de orientacion // Vector3, 3 observaciones
        sensor.AddObservation(Energy / MaxEnergy);
    }
    public void MoveAgent(ActionSegment<int> act)
    {
        var dirToGo = Vector3.zero;
        var rotateDir = Vector3.zero;

        var action = act[0];
        switch (action)
        {
            case 0:// no hace nada
                Energy -= m_RealLossEnergy * 0.1f;
                break;
            case 1:
                dirToGo = transform.forward * 1f;
                Energy -= m_RealLossEnergy;
                break;
            case 2:
                dirToGo = transform.forward * -1f;
                Energy -= m_RealLossEnergy;
                break;
            case 3:
                rotateDir = transform.up * 1f;
                Energy -= m_RealLossEnergy;
                break;
            case 4:
                rotateDir = transform.up * -1f;
                Energy -= m_RealLossEnergy;
                break;
        }
        transform.Rotate(rotateDir, Time.deltaTime * 200f);

        agentRb.AddForce(dirToGo * MovSpeed, ForceMode.VelocityChange);
    }
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        AddReward(m_Existential);
        if (Energy <= 0)
        {
            AgentExhaustion();
        }
        MoveAgent(actionBuffers.DiscreteActions); // recibe 1 vector de 5 observaciones 
    }
    private void ResetState() // timer state
    {
        state = false;
        m_RealLossEnergy = LossEnergy;
        m_Renderer.material.color = Color.white;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        if (Input.GetKey(KeyCode.D))
        {
            discreteActionsOut[0] = 3;
        }
        else if (Input.GetKey(KeyCode.W))
        {
            discreteActionsOut[0] = 1;
        }
        else if (Input.GetKey(KeyCode.A))
        {
            discreteActionsOut[0] = 4;
        }
        else if (Input.GetKey(KeyCode.S))
        {
            discreteActionsOut[0] = 2;
        }
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (team == Team.Hunted)
        {
            if (state == false && collision.gameObject.CompareTag("Target")) // cuando toma el target
            {
                CollisionBehavior();
                //AddReward(0.2f);
                m_envController.DespawnTarget(collision.gameObject);
            }
        }
        else
        {
            if (state == false && collision.gameObject.CompareTag("hunted")) // el cazador atrapa
            {
                CollisionBehavior();
                //AddReward(0.2f); // premio
                // comportamiento hunted
                MultiAgentv3 agentHunted = collision.gameObject.GetComponent<MultiAgentv3>();

                agentHunted.DevourAgent();
            }
        }
    }
    private void CollisionBehavior()
    {
        state = true;
        Energy = MaxEnergy;
        m_Renderer.material.color = colorState;
        m_RealLossEnergy = 0; // al "comer" deja de perder energia
        //m_envController.RewardGroup(team);
        AddReward(0.2f); // premio
        m_FoodCount++;
        if (m_FoodCount >= reproductionRate)
        {
            GameObject gameObjectAgent = m_envController.SpawnAgent(team, transform.position); // duplica agente 
            if (gameObjectAgent != null)
            {
                m_FoodCount = 0;
            }
        }
        m_TimeManager.RegisterTimerEvent(IntervalTimerState, ResetState); // timer state
    }
    // reespawn
    private void AgentExhaustion() // cuando un agente muere por falta de energia
    {
        InactivateAgent();
        m_envController.SpawnCarrion(transform.position);
    }
    private void DevourAgent() // cuando un agente es comido
    {
        InactivateAgent();
        m_envController.AddFertilizer(transform.position);
    }
    private void InactivateAgent()
    {
        AddReward(-0.5f);
        ResetAgent();
        m_envController.DespawnAgent(this);
    }
    public void ResetAgent()
    {
        agentRb.angularVelocity = Vector3.zero;
        agentRb.velocity = Vector3.zero;
        state = false;
        Energy = m_InitialEnergy;
        m_Renderer.material.color = Color.white;
        m_RealLossEnergy = LossEnergy;
        m_FoodCount = 0;
    }

    // end respawn
    //public override void OnEpisodeBegin()
    //{

    //}
}
