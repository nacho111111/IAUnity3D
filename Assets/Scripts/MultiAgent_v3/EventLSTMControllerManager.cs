using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class EventLSTMControllerManager : MonoBehaviour
{
    // -------- CONFIGURACIÓN --------
    public float detectionRadius = 10f;
    public float forgetTime = 10f;      // tiempo maximo que recuerda una entidad

    public LayerMask detectableLayers; // Hunted, Hunter, etc.

    private Collider[] hitColliders = new Collider[50]; // buffer para OverlapSphere

    private StatsRecorder m_stats; // para graficos
    private TimerManager m_TimeManager;

    // -------- MEMORIA DE ENTIDADES --------
    private Dictionary<string, MemoryData> m_memory = new();

    // -------- VARIABLES DE ESTADO PROPIO --------

    private float m_timeSinceLastInteraction;
 

    // --------- MEMORY ---------
    private class MemoryData
    {
        public Vector3 exitDirection;
        public float timeSinceExit;
    }   

    // --------- Population STRUCT ---------
    private struct PopulationSnapshot
    {
        public int hunted;
        public int hunter;
        public int generator;
        public int target;
    }

    private PopulationSnapshot currentPopulation;  // actual


    private void Awake()
    {
        m_stats = Academy.Instance.StatsRecorder;
        m_TimeManager = GetComponentInParent<TimerManager>();
    }
    private void OnEnable()
    {
        m_TimeManager.RegisterTimerEvent(1, RegisterStats);
        m_timeSinceLastInteraction = 0f;
    }

    private void Update()
    {
        m_timeSinceLastInteraction += Time.deltaTime;
        UpdateMemoryTimers();
    }

    private void UpdatePopulation()
    {
        currentPopulation = new PopulationSnapshot();

        int count = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, hitColliders, detectableLayers);
        
        for (int i = 0; i < count; i++)
        {
            GameObject obj = hitColliders[i].gameObject;

            //if (obj == gameObject)
            //    continue;
            
            switch (obj.tag)
            {
                case "hunted":
                    currentPopulation.hunted++;
                    break;

                case "hunter":
                    currentPopulation.hunter++;
                    break;

                case "Generator":
                    currentPopulation.generator++;
                    break;

                case "Target":
                    currentPopulation.target++;
                    break;
            }
        }
    }
    private void RegisterStats()
    {
        
        float hunterToHunted = currentPopulation.hunter / (float)(currentPopulation.hunted + 1);
        m_stats.Add("LSTM/LocalHunterHuntedRatio",hunterToHunted,StatAggregationMethod.Average);

        m_stats.Add("LSTM/LocalHunted", currentPopulation.hunted, StatAggregationMethod.Average);
        m_stats.Add("LSTM/LocalHunter", currentPopulation.hunter, StatAggregationMethod.Average);

        m_stats.Add("LSTM/MemoryEntries", m_memory.Count, StatAggregationMethod.Average); // cuanta memoria tienen los agentes


        if (m_memory.Count > 0)
        {
            float avgMemoryAge = m_memory.Values.Average(x => x.timeSinceExit);

            m_stats.Add("LSTM/MemoryAge", avgMemoryAge, StatAggregationMethod.Average);
        }

        m_TimeManager.RegisterTimerEvent(1, RegisterStats);
    }
    private void UpdateMemoryTimers()
    {
        List<string> toForget = null;

        foreach (var kvp in m_memory)
        {
            kvp.Value.timeSinceExit += Time.deltaTime; // tiempo desde la salida del area


            if (kvp.Value.timeSinceExit > forgetTime) // si tiene que olvidarla agrega a lista
            {
                toForget ??= new List<string>();
                toForget.Add(kvp.Key);
            }
        }

        if (toForget != null)                       // olvida si esta en la lista
        {
            foreach (var key in toForget)
                m_memory.Remove(key);
        }

    }

    private void OnTriggerExit(Collider other) // recuerda la ultima entidad del tipo "tag" que salio del area
    {
        if (other.isTrigger)
            return;
        string tag = other.tag;

        if (tag != "hunted" &&
            tag != "hunter" &&
            tag != "Generator" &&
            tag != "Target")
            return;

        
        Vector3 dir = transform.InverseTransformDirection((other.transform.position - transform.position).normalized);
        m_memory[tag] = new MemoryData
        {
            exitDirection = dir,
            timeSinceExit = 0f
        };
    }

    public void WriteObservations(VectorSensor sensor, Vector3 velocity, float energyChangeRate, float energyPercent)
    {
        UpdatePopulation();

        // -------- ESTADO PROPIO --------

        sensor.AddObservation(transform.InverseTransformDirection(velocity)); // 3
        sensor.AddObservation(energyChangeRate);                              // 1
        sensor.AddObservation(Mathf.Clamp01(m_timeSinceLastInteraction / 400f));                // 1
        sensor.AddObservation(energyPercent);
        // = 6

        // -------- CONTEOS LOCALES --------
        float maxPopEst = 50f;
        sensor.AddObservation(currentPopulation.hunted / maxPopEst);
        sensor.AddObservation(currentPopulation.hunter / maxPopEst);
        sensor.AddObservation(currentPopulation.generator / maxPopEst);
        sensor.AddObservation(currentPopulation.target / maxPopEst);   // = 4

        // -------- RATIO LOCAL --------
        int totalPop = currentPopulation.hunter + currentPopulation.hunted;

        if (totalPop == 0)
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
        else
        {
            sensor.AddObservation(currentPopulation.hunter / (float)totalPop);
            sensor.AddObservation(currentPopulation.hunted / (float)totalPop);
        }

        // -------- MEMORY --------

        foreach (string type in new[]
        {
            "hunted",
            "hunter",
            "Generator",
            "Target"
        })
        {
            if (m_memory.TryGetValue(type, out var data))
            {
                sensor.AddObservation(data.exitDirection.x);
                sensor.AddObservation(data.exitDirection.z);
                sensor.AddObservation(Mathf.Clamp01(data.timeSinceExit / forgetTime));
            }
            else
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(1f);
            }
        }      // 4 * 3 = 12, 12 + 11 = total 23 obs
    }
    public void ReseTimeLastInteraction()
    {
        m_timeSinceLastInteraction = 0f;
    }
    public (int,int) GetPopulation()
    {
        return (currentPopulation.hunted, currentPopulation.hunter);
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(new Vector3(transform.position.x, transform.position.y, transform.position.z), detectionRadius);
    }
}