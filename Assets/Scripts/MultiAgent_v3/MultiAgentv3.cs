using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEngine.UIElements;
using System;
using System.Collections;
using Unity.MLAgents.Sensors;
using static UnityEngine.GraphicsBuffer;
using System.Collections.Generic;
using Unity.VisualScripting;

public class MultiAgentv3 : Agent
{
    // -------- CONFIGURACIÓN --------
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

    private float m_previousEnergy;

    // state
    public bool state;  // estado, si ha comido o no
    [HideInInspector] public float IntervalTimerState;
    // end state

    // --------- references ---------
    private Renderer m_Renderer;
    [HideInInspector] public Rigidbody agentRb;
    private EnvControllerv3 m_envController;
    BehaviorParameters m_BehaviorParameters; // mlAgent
    private TimerManager m_TimeManager;
    private StatsRecorder m_stats; // para graficos
    private EventLSTMControllerManager m_eventManager;

    protected new void OnEnable()
    {
        base.OnEnable();  
    }
    public override void Initialize()
    {
        agentRb = GetComponent<Rigidbody>();
        m_Renderer = GetComponentInParent<Renderer>();
        m_envController = gameObject.GetComponentInParent<EnvControllerv3>();
        m_TimeManager = GetComponentInParent<TimerManager>();
        m_stats = Academy.Instance.StatsRecorder;
        m_eventManager = GetComponent<EventLSTMControllerManager>();

        m_Existential = 1f / m_envController.MaxEnvironmentSteps;
        m_RealLossEnergy = LossEnergy;
        m_InitialEnergy = InitialEnergy * MaxEnergy;
        Energy = m_InitialEnergy;//



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
        float energyChangeRate = ((Energy - m_previousEnergy) / MaxEnergy) / Time.fixedDeltaTime;   // velocidad a la que gana/pierde energía
        float energyPercent = Energy / MaxEnergy;                                                   // energia 0 a 1
        m_previousEnergy = Energy;

        Vector3 velocity = transform.InverseTransformDirection(agentRb.velocity);

        m_eventManager.WriteObservations(sensor, velocity, energyChangeRate, energyPercent);

        //sensor.AddObservation(state); // bool, 1 observacion
        //sensor.AddObservation(transform.InverseTransformDirection(agentRb.velocity)); // referencia de orientacion // Vector3, 3 observaciones
        //sensor.AddObservation(Energy / MaxEnergy);
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
        //AddReward(m_Existential); // esta linea se traslada a envcontroller l 165
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

    public override void Heuristic(in ActionBuffers actionsOut) // estado manual, no ml
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
                m_stats.Add("Consumption/HunterEnergy", Energy, StatAggregationMethod.Average);

                CollisionEatBehavior();
                m_envController.DespawnTarget(collision.gameObject);
                m_envController.TargetsEatenCount();
            }
        }
        else
        {
            if (state == false && collision.gameObject.CompareTag("hunted")) // el cazador atrapa
            {
                m_stats.Add("Consumption/HuntedEnergy", Energy, StatAggregationMethod.Average);

                CollisionEatBehavior();
                // comportamiento hunted
                MultiAgentv3 agentHunted = collision.gameObject.GetComponent<MultiAgentv3>();

                agentHunted.DevourAgent();
            }
        }
        if (collision.gameObject.CompareTag("borders")) // chocar con las paredes
        {
            AddReward(-0.001f);
        }
    }
    private void CollisionEatBehavior()
    {
        state = true;
        m_eventManager.ReseTimeLastInteraction();

        m_Renderer.material.color = colorState;
        m_RealLossEnergy = 0; // al "comer" deja de perder energia por un tiempo "IntervalTimerState"
        float reward;                  //m_envController.RewardGroup(team);
        float energyPercent = Energy / MaxEnergy;
        if (team == Team.Hunted)
        {
            reward = 1f - energyPercent;
        }
        else
        {
            //if (EnergyP < 0.30f)
            //{
            //    // comer con hambre
            //    reward = 2.5f;
            //}
            ////else if (EnergyP > 0.75f)
            ////{
            ////    // Caza innecesaria. Castigo severo porque pone en riesgo el ecosistema
            ////    reward = - 2.0f;
            ////}
            //else
            //{
            //    reward  = 0f;
            //}
            float hungerReward = 3f * Mathf.Pow(1f - energyPercent, 2f); // recompensa exponencial por hambre
            float sustainabilityPenalty = 0f;

            (int hunterPo, int huntedPo) = m_eventManager.GetPopulation();
            if (hunterPo > huntedPo)
            {
                sustainabilityPenalty = -0.5f;                  // castigo por desequilibrio poblacional
            }
            reward = hungerReward + sustainabilityPenalty;
            //Debug.Log($"Hunter comió. Hambre: {hungerReward:F2} | Penalización: {sustainabilityPenalty:F2} | Total: {reward:F2}");
        }
        //float reward = Mathf.Lerp(1f, 0.5f, Energy / MaxEnergy); // + comer 
        AddReward(reward); // recompensa depende de cuanta energia tiene el agente
        Energy = MaxEnergy;

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

        if (team == Team.Hunted)
        {
            m_stats.Add("Agent/cantidad", m_envController.CountHunted, StatAggregationMethod.MostRecent);
        }
        else
        {
            m_stats.Add("Agent/cantidad", m_envController.CountHunter, StatAggregationMethod.MostRecent);
        }
    }
    private void DevourAgent() // cuando un agente es comido
    {
        InactivateAgent();
        m_envController.AddFertilizer(transform.position);

        m_stats.Add("Agent/cantidad", m_envController.CountHunted, StatAggregationMethod.MostRecent);
    }
    private void InactivateAgent()
    {
        float reward;

        if (team == Team.Hunted)
        {
            reward = -1.0f;
        }
        else
        {
            reward = -1.5f;
        }
        AddReward(reward);
        //AddReward(-0.5f); // + sobrevivencia, - equilibrio  
        ResetAgent();
        m_envController.DespawnAgent(this);
        m_envController.VerifyAgents();
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
