using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MemoryManager : MonoBehaviour
{
    private int m_HuntedCount;
    private int m_HunterCount;
    private int m_GeneratorCount;
    private int m_TargetCount;

    // memory data 

    [System.Serializable]
    public struct MemoryData
    {
        public GameObject entity;
        public Vector3 exitDirection;
        public float timeSinceLastSeen;

        public static MemoryData Empty => new MemoryData
        {
            entity = null,
            exitDirection = Vector3.zero,
            timeSinceLastSeen = 0f
        };
    }
    public enum MemoryType
    {
        Hunted,
        Hunter,
        Generator,
        Target
    }
    private Dictionary<MemoryType, MemoryData> memory = new();
    private readonly List<MemoryType> tempKeysToForget = new List<MemoryType>(4);

    void Start()
    {
        // Inicializar 
        foreach (MemoryType type in System.Enum.GetValues(typeof(MemoryType)))
        {
            memory[type] = MemoryData.Empty;
        }
    }
    void Update()
    {
        float deltaTime = Time.deltaTime;

        // Solo  4 elementos máximo 
        tempKeysToForget.Clear();

        foreach (var kvp in memory)
        {
            // Crear una copia del struct, modificarlo y volver a asignarlo
            MemoryData updatedData = kvp.Value;
            updatedData.timeSinceLastSeen += deltaTime;

            if (updatedData.timeSinceLastSeen > 10f)
            {
                tempKeysToForget.Add(kvp.Key);
            }
            else
            {
                // Actualizar el valor en el dictionary
                memory[kvp.Key] = updatedData;
            }
        }

        // Limpiar las memorias que expiraron
        for (int i = 0; i < tempKeysToForget.Count; i++)
        {
            ClearMemory(tempKeysToForget[i]);
        }
    }

    void UpdateMemory(MemoryType type, Collider other) // se usa cuando sale una entidad del area
    {
        Vector3 exitDir = (other.transform.position - transform.position).normalized;

        memory[type] = new MemoryData
        {
            entity = other.gameObject,
            exitDirection = exitDir,
            timeSinceLastSeen = 0f
        };
    }

    public void ClearMemory(MemoryType type)
    {
        memory[type] = MemoryData.Empty;
    }

    private void OnTriggerEnter(Collider other)
    {
        string tag = other.tag;

        if (tag == "Hunted")
            m_HuntedCount++;
        else if (tag == "Hunter")
            m_HunterCount++;
        else if (tag == "Generator")
            m_GeneratorCount++;
        else if (tag == "Target")
            m_TargetCount++;
    }

    void OnTriggerExit(Collider other)
    {
        string tag = other.tag;

        if (tag == "Hunted"){
            UpdateMemory(MemoryType.Hunted, other);
            m_HuntedCount--;
        }
        else if (tag == "Hunter") {
            UpdateMemory(MemoryType.Hunter, other);
            m_HunterCount--;
        }
       
        else if (tag == "Generator"){
            UpdateMemory(MemoryType.Generator, other);
            m_GeneratorCount--;
        }
        else if (tag == "Target"){
            UpdateMemory(MemoryType.Target, other);
            m_TargetCount--;
        }
    }
    

    
}
