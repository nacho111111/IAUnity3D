using Palmmedia.ReportGenerator.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEditor.Progress;

//using static UnityEditor.Progress;
//using static UnityEditor.Timeline.Actions.MenuPriority;
using Random = UnityEngine.Random;

public class EnvControllerv3 : MonoBehaviour
{
    //[System.Serializable]
    //public class PlayerInfo
    //{
    //    public MultiAgentv3 Agent;
    //[HideInInspector]
    //public Rigidbody Rb;
    //}
    [System.Serializable]
    public class SpawnInfo
    {
        public Transform Tr;
        [HideInInspector]
        public Vector3 Vector;
    }


    [Tooltip("Max Environment Steps")] public int MaxEnvironmentSteps = 25000;

    [Header("Agentes")]

    [SerializeField] private bool randomize = false;

    [SerializeField] private Color m_colorHuntedState;
    [SerializeField] private Color m_colorHunterState;

    [SerializeField, Range(0, 10)] private int m_HowGenerator;

    [SerializeField, Range(0, 10)] private int m_HowHunted;
    [SerializeField, Range(0, 10)] private int m_HowHunter;

    [SerializeField, Range(5, 10)] private int m_MaxHunted;
    [SerializeField, Range(5, 10)] private int m_MaxHunter;

    [SerializeField, Range(1, 5)] private float m_MovSpeedHunted = 2;
    [SerializeField, Range(1, 5)] private float m_MovSpeedHunter = 2;

    [SerializeField, Range(10, 200)] private float m_MaxEnergyHunted = 100;
    [SerializeField, Range(10, 200)] private float m_MaxEnergyHunter = 150;

    [SerializeField, Range(0, 1)] private float m_InitialEnergyHunted = 1;
    [SerializeField, Range(0, 1)] private float m_InitialEnergyHunter = 1;

    [SerializeField, Range(0.01f, 0.2f)] private float m_LossEnergyHunted = 0.1f;
    [SerializeField, Range(0.01f, 0.2f)] private float m_LossEnergyHunter = 0.1f;

    [Tooltip("Cada cuantas comidas se reproduce el hunted"), SerializeField, Range(1, 10)] private int m_reproRateHunted = 2;
    [Tooltip("Cada cuantas comidas se reproduce el hunter"), SerializeField, Range(1, 10)] private int m_reproRateHunter = 2;

    [Tooltip("Tiempo entre comidas del hunted"), SerializeField, Range(0, 10)] private float m_TimerStateHunted = 2;
    [Tooltip("Tiempo entre comidas del hunter"), SerializeField, Range(0, 10)] private float m_TimerStateHunter = 2;

    private SimpleMultiAgentGroup m_HuntedGroup;
    private SimpleMultiAgentGroup m_HunterGroup;

    private int m_ResetTimer;
    [Header("Generator")]

    [SerializeField, Range(5, 30)] private float m_GrowthTime;
    [SerializeField, Range(10, 120)] private float m_DespawnTime;
    [SerializeField, Range(1, 20)] private int m_SpawnConut;
    [Space(15)]
    [SerializeField, Range(0, 1)] private float m_FoodSpawnRate;
    [SerializeField, Range(0, 1)] private float m_GeneratorSpawnRate;
    [SerializeField, Range(0, 1)] private float m_CarriorSpawnRate;
    [SerializeField] private Material m_PostMaterial;

    [Header("Listas")]

    //public List<PlayerInfo> AgentsBase = new List<PlayerInfo>();
    //public List<GameObject> ObjectBase = new List<GameObject>();
    public int ObjectBaseCount;
    //public List<TargetInfo> TargetList = new List<TargetInfo>();
    public List<SpawnInfo> SpawnList = new List<SpawnInfo>();
    private Stack<GameObject> IdleHuntedStack = new Stack<GameObject>();
    private Stack<GameObject> IdleHunterStack = new Stack<GameObject>();
    private Stack<GameObject> IdleTargetStack = new Stack<GameObject>();
    private Stack<GameObject> IdleGeneratorStack = new Stack<GameObject>();
    private Stack<GameObject> IdleFertilizerStack = new Stack<GameObject>();
    private Stack<GameObject> IdleCarrionStack = new Stack<GameObject>();

    //private Dictionary<GameObject, GameObject> ActiveHunteds = new Dictionary<GameObject, GameObject>();
    //private Dictionary<GameObject, GameObject> ActiveHunters = new Dictionary<GameObject, GameObject>();
    private Dictionary<GameObject, GameObject> ActiveObjects = new Dictionary<GameObject, GameObject>();



    [Header("Prefavs")]

    [SerializeField] private GameObject m_PrefavHunted;
    [SerializeField] private GameObject m_PrefavHunter;
    [SerializeField] private GameObject m_PrefavGenerator;
    private float prefavGeneratorY;
    [SerializeField] private GameObject m_PrefavTarget;
    [SerializeField] private GameObject m_PrefavFertilizer;
    [SerializeField] private GameObject m_PrefavCarrion;

    // agentes totales
    public int CountHunted;
    public int CountHunter;

    //private int m_CountAgents; // contador agentes iniciales
    private int m_CountSpawns;

    // stats
    private int m_TargetsEaten;
    private int m_huntedsEaten;
    private int m_GeneratorsSpawns;
    // end stats

    private TimerManager m_TimeManager;
    private StatsRecorder stats;
    private void OnValidate()
    {
        if (m_GrowthTime >= m_DespawnTime - 10)
        {
            m_GrowthTime = m_DespawnTime - 10;
            Debug.LogWarning("El tiempo de despawn tiene que ser considerablemente mayor al de crecimiento. Se ha ajustado automáticamente.");
        }
    }
    //////////////////
    /// Initialize ///
    //////////////////

    void Start()
    {
        prefavGeneratorY = m_PrefavGenerator.transform.position.y; // evita hacer esta consulta constantemente
        m_TimeManager = GetComponent<TimerManager>();
        stats = Academy.Instance.StatsRecorder;

        // Initialize TeamManager
        m_HuntedGroup = new SimpleMultiAgentGroup();
        m_HunterGroup = new SimpleMultiAgentGroup();

        // Initialize count
        m_CountSpawns = SpawnList.Count;
        ObjectBaseCount = m_HowHunted + m_HowHunter + m_HowGenerator;

        m_TimeManager.RegisterTimerEvent(5, StatRegistarAgents);

        foreach (var item in SpawnList)
        {
            item.Vector = item.Tr.position;
        }
        ResetScene();
    }
    void FixedUpdate()
    {

        float Existential = 1f / MaxEnvironmentSteps; // recompensa por perdurar el sistema
        m_HuntedGroup.AddGroupReward(Existential);
        m_HunterGroup.AddGroupReward(Existential);

        m_ResetTimer += 1;
        if (m_ResetTimer >= MaxEnvironmentSteps && MaxEnvironmentSteps > 0) // si se llega al maximo tiempo hay premio
        {
            m_HuntedGroup.AddGroupReward(2);
            m_HunterGroup.AddGroupReward(2);

            m_HuntedGroup.EndGroupEpisode();
            m_HunterGroup.EndGroupEpisode();
            ResetScene();
        }
    }
    private void InitializeHunted(MultiAgentv3 hunted)
    {
        hunted.colorState = m_colorHuntedState;
        hunted.MaxEnergy = m_MaxEnergyHunted;
        hunted.InitialEnergy = m_InitialEnergyHunted;
        hunted.LossEnergy = m_LossEnergyHunted;
        hunted.MovSpeed = m_MovSpeedHunted;
        hunted.reproductionRate = m_reproRateHunted;
        hunted.IntervalTimerState = m_TimerStateHunted;
    }
    private void InitializeHunter(MultiAgentv3 hunter)
    {
        hunter.colorState = m_colorHunterState;
        hunter.LossEnergy = m_LossEnergyHunter;
        hunter.MaxEnergy = m_MaxEnergyHunter;
        hunter.InitialEnergy = m_InitialEnergyHunter;
        hunter.MovSpeed = m_MovSpeedHunter;
        hunter.reproductionRate = m_reproRateHunter;
        hunter.IntervalTimerState = m_TimerStateHunter;
    }
    public void InitializeGenerator(Generatorv2 generator)
    {
        generator.DespawnTime = m_DespawnTime;
        generator.GrowthTime = m_GrowthTime;
        generator.SpawnRate = m_FoodSpawnRate;
        generator.SpawnCount = m_SpawnConut;
        generator.PostMaterial = m_PostMaterial;
    }
    private void Randomizer()
    {
        m_HowGenerator = Random.Range(1, 10);
        m_HowHunted = Random.Range(1, 10);
        m_HowHunter = Random.Range(1, 10);
        m_MovSpeedHunted = Random.Range(1, 5);
        m_MovSpeedHunter = Random.Range(1, 5);
        m_MaxEnergyHunted = Random.Range(50,200);
        m_MaxEnergyHunter = Random.Range(50, 200);

        ObjectBaseCount = m_HowHunted + m_HowHunter + m_HowGenerator;
    }

    ///////////////
    /// Rewards ///
    ///////////////

    //public void RewardGroup(Team team)
    //{
    //    if (team == Team.Hunted)
    //    {
    //        m_HuntedGroup.AddGroupReward(1 - (float)m_ResetTimer / MaxEnvironmentSteps);
    //        scoreHunted++;
    //        if (scoreHunted == 3)
    //        {
    //            Debug.Log("Hunted Group Reward");
    //            m_HuntedGroup.AddGroupReward(2 - (float)m_ResetTimer / MaxEnvironmentSteps);
    //            m_HunterGroup.AddGroupReward(-1);
    //            m_HuntedGroup.EndGroupEpisode();
    //            m_HunterGroup.EndGroupEpisode();
    //            //ResetScene();
    //        }
    //    }
    //    else
    //    {
    //        m_HunterGroup.AddGroupReward(1 - (float)m_ResetTimer / MaxEnvironmentSteps);
    //        scoreHunter++;
    //        if (scoreHunter == 2)
    //        {
    //            Debug.Log("Hunter Group Reward");
    //            m_HunterGroup.AddGroupReward(2 - (float)m_ResetTimer / MaxEnvironmentSteps);
    //            m_HuntedGroup.AddGroupReward(-1);
    //            m_HuntedGroup.EndGroupEpisode();
    //            m_HunterGroup.EndGroupEpisode();
    //            //ResetScene();
    //        }
    //        //HowManyHunted();
    //    }
    //}

    //////////////////
    /// contadores ///
    //////////////////

    //public bool ExistsAgents() // quedan agentes?
    //{
    //    if (CountHunted + CountHunter <= 0)
    //    {
    //        return false;
    //    }
    //    return true;
    //}

    private void StatRegistarAgents()
    {
        stats.Add("Agent/cantidad", CountHunted);
        
        stats.Add("Agent/cantidad", CountHunter);

        m_TimeManager.RegisterTimerEvent(5, StatRegistarAgents);
    }
    public void VerifyAgents()
    {
        //if (CountHunted + CountHunter <= 0) // cuando no quedan mas agentes 
        //{
        //    InterrupredEpisode();
        //}
        if (CountHunted <= 0 || CountHunter <= 0) // cuando no quedan mas agentes 
        {
            InterrupredEpisode();
        }

    }
    private bool CountTeam(Team team, int num) // lleva los contadores e itera segun su team
    {
        if (team == Team.Hunted)
        {
            if (CountHunted + num > m_MaxHunted)
            {
                return false;
            }
            CountHunted += num;
        }
        else
        {
            if (CountHunter + num > m_MaxHunter)
            {
                return false;
            }
            CountHunter += num;
        }
        //stats.Add("Agent/cantidad hunted", CountHunted);
        //stats.Add("Agent/cantidad hunter", CountHunter);
        return true;
    }
    public void TargetsEatenCount()
    {
        m_TargetsEaten++;
    }

    //////////////
    /// spawns ///
    //////////////

    // agent
    public GameObject SpawnAgent(Team team, Vector3 position = default(Vector3) )
    {
        if (!CountTeam(team, 1))
        {
            return null;
        }

        GameObject gameObject;

        if (team == Team.Hunted)
        {
            if (IdleHuntedStack.Count > 0)
            {
                gameObject = IdleHuntedStack.Pop();
                gameObject.transform.position = position;
            }
            else
            {
                gameObject = Instantiate(m_PrefavHunted, position, Quaternion.identity, transform);
                InitializeHunted(gameObject.GetComponent<MultiAgentv3>());
            }


            if (!ActiveObjects.ContainsKey(gameObject)) // error posible 
            {
                ActiveObjects.Add(gameObject, gameObject); // add diccionary Actives
            }
            else
            {
                Debug.LogWarning($"El objeto hunted {gameObject.name} ya está en ActiveObjects.");
            }


            MultiAgentv3 agent = gameObject.GetComponent<MultiAgentv3>();
            if (randomize)
            {
                agent.MovSpeed = m_MovSpeedHunted;
                agent.MaxEnergy = m_MaxEnergyHunted;
            }
            gameObject.SetActive(true);
            m_HuntedGroup.RegisterAgent(agent);
        }
        else
        {
            if (IdleHunterStack.Count > 0)
            {
                gameObject = IdleHunterStack.Pop();
                gameObject.transform.position = position;
            }
            else
            {
                gameObject = Instantiate(m_PrefavHunter, position, Quaternion.identity, transform);
                InitializeHunter(gameObject.GetComponent<MultiAgentv3>());
            }

            if (!ActiveObjects.ContainsKey(gameObject)) // error posible 
            {
                ActiveObjects.Add(gameObject, gameObject); // add diccionary Actives
            }
            else
            {
                Debug.LogWarning($"El objeto hunter {gameObject.name} ya está en ActiveObjects.");
            }

            MultiAgentv3 agent = gameObject.GetComponent<MultiAgentv3>();
            if (randomize)
            {
                agent.MovSpeed = m_MovSpeedHunter;
                agent.MaxEnergy = m_MaxEnergyHunter;
            }
            gameObject.SetActive(true);
            m_HunterGroup.RegisterAgent(agent);
        }
        return gameObject;
    }
    public void DespawnAgent(MultiAgentv3 Agent)
    {
        CountTeam(Agent.team, -1);
        GameObject gameObject = Agent.gameObject;
        if (Agent.team == Team.Hunted)
        {
            ActiveObjects.Remove(gameObject);
            gameObject.SetActive(false);
            IdleHuntedStack.Push(gameObject);
        }
        else
        {
            ActiveObjects.Remove(gameObject);
            gameObject.SetActive(false);
            IdleHunterStack.Push(gameObject);
        }
    }
    // end Agent
    
    // fertilizer
    public void AddFertilizer(Vector3 position) // se llama cuando un hunted es comido
    {
        m_huntedsEaten++;
        float ran = Random.value;
        if (ran < m_GeneratorSpawnRate)
        {
            SpawnGenerator(position);
        }
        SpawnFertilizer(position);
    }
    private void SpawnFertilizer(Vector3 position)
    {
        GameObject gameObject;
        if (IdleFertilizerStack.Count > 0)
        {
            gameObject = IdleFertilizerStack.Pop();
            gameObject.SetActive(true);
            //gameObject.transform.position = position + new Vector3(0,-0.56f,0);
            gameObject.transform.position = position;
        }
        else
        {
            gameObject = Instantiate(m_PrefavFertilizer, position, Quaternion.identity, transform);
        }
        ActiveObjects.Add(gameObject,gameObject);
    }
    public void DespawnFertilizer(GameObject Fertilizer)
    {
        Fertilizer.SetActive(false);
        ActiveObjects.Remove(Fertilizer);
        IdleFertilizerStack.Push(Fertilizer);
    }
    // end fertilizer

    // Carrion
    public void SpawnCarrion(Vector3 position)
    {
        GameObject gameObject;
        float ran = Random.value;
        if (ran < m_CarriorSpawnRate)
        {
            if (IdleCarrionStack.Count > 0)
            {
                gameObject = IdleCarrionStack.Pop();
                gameObject.SetActive(true);
                gameObject.transform.position = position;
            }
            else
            {
                gameObject = Instantiate(m_PrefavCarrion, position, Quaternion.identity, transform);
            }
            ActiveObjects.Add(gameObject, gameObject);
        }
    }
    public void DespawnCarrion(GameObject Carrion)
    {
        Carrion.SetActive(false);
        ActiveObjects.Remove(Carrion);
        IdleCarrionStack.Push(Carrion);
    }
    // end Carrion

    // generator
    private void SpawnGenerator(Vector3 position)
    {
        m_GeneratorsSpawns++;
        GameObject gameObject;
        if (IdleGeneratorStack.Count > 0)
        {
            gameObject = IdleGeneratorStack.Pop();
            gameObject.SetActive(true);
            gameObject.transform.position = new Vector3(position.x, prefavGeneratorY, position.z);
        }
        else
        {
            gameObject = Instantiate(m_PrefavGenerator, new Vector3(position.x, prefavGeneratorY, position.z), Quaternion.identity, transform);
            //InitializeGenerator(gameObject.GetComponent<Generatorv2>()); // se llama en generator par evitar errores en el tiempo de ejecucion
        }
        ActiveObjects.Add(gameObject, gameObject);
    }
    public void DespawnGenerator(GameObject generator)
    {
        generator.SetActive(false);
        ActiveObjects.Remove(generator);
        IdleGeneratorStack.Push(generator);
    }
    // end generator

    // target
    public void SpawnTarget(Vector3 position)
    {
        GameObject gameObject;
        int ran1 = Random.Range(-5, 5);
        int ran2 = Random.Range(-5, 5);
        if (IdleTargetStack.Count > 0)
        {
            gameObject = IdleTargetStack.Pop();
            gameObject.SetActive(true);
            gameObject.transform.position = position + new Vector3(ran1, 5, ran2);
        }
        else
        {
            gameObject = Instantiate(m_PrefavTarget, position + new Vector3(ran1, 5, ran2), Quaternion.identity, transform);
        }
        ActiveObjects.Add(gameObject, gameObject);
        //Debug.Log("se genera 1 target* " + ActiveObjects.Count);
    }
    public void DespawnTarget(GameObject target)
    {
        target.GetComponent<Targetv3>().ResetTarget();
        target.SetActive(false);
        ActiveObjects.Remove(target);
        IdleTargetStack.Push(target);
        //Debug.Log("se elimina 1 target* " + ActiveObjects.Count);

    }
    // end target

    //////////////
    /// resets ///
    //////////////

    private void InterrupredEpisode()
    {
        m_HuntedGroup.AddGroupReward((-1 + (float)m_ResetTimer / MaxEnvironmentSteps) * 1);
        m_HunterGroup.AddGroupReward((-1 + (float)m_ResetTimer / MaxEnvironmentSteps) * 1);

        m_HuntedGroup.GroupEpisodeInterrupted();
        m_HunterGroup.GroupEpisodeInterrupted();
        stats.Add("Timer/Tiempo en colapsar",m_ResetTimer);

        ResetScene();
    }
    private void ResetObject()
    {
        List<GameObject> objetosAEliminar = new List<GameObject>(ActiveObjects.Values);

        foreach (var obj in objetosAEliminar)
        {
            if (obj.CompareTag("hunted") || obj.CompareTag("hunter"))
            {
                MultiAgentv3 agent = obj.GetComponent<MultiAgentv3>();
                agent.ResetAgent();
                DespawnAgent(agent);

            }
            else if (obj.CompareTag("Target"))
            {
                DespawnTarget(obj);
            }
            else if (obj.CompareTag("Generator"))
            {
                DespawnGenerator(obj);
            }
            else if (obj.CompareTag("Fertilizer"))
            {
                DespawnFertilizer(obj);
            }
            else if (obj.CompareTag("Carrion"))
            {
                DespawnCarrion(obj);
            }
            else
            {
                Debug.LogWarning("No se encontro el tag en reset de objetos");
            }
        }
        ActiveObjects.Clear();
    }
    //private void resetObject(GameObject obj, Vector3 position, Quaternion rotation)
    //{
    //    obj.SetActive(true);
    //    obj.transform.position = position;
    //    obj.transform.rotation = rotation;
    //    ActiveObjects.Add(obj,obj);

    //}
    public void ResetScene()
    {
        if (randomize)
        {
            Randomizer();
        }

        stats.Add("hunted/Cantidad target comidos", m_TargetsEaten);
        stats.Add("hunter/Cantidad hunted comidos", m_huntedsEaten);
        stats.Add("generator/Cantidad generators instanciados", m_GeneratorsSpawns);
        
        m_TargetsEaten = 0;
        m_huntedsEaten = 0;
        m_GeneratorsSpawns = 0;

        m_TimeManager.ClearAllTimerEvents();
        m_ResetTimer = 0;
        int iterInd = 0;
        //Debug.Log("existen " + ActiveObjects.Count + " al final del episodio");
        ResetObject();

        var randomIndices = Enumerable.Range(0, m_CountSpawns)
                                    .OrderBy(x => Guid.NewGuid())
                                    .Take(ObjectBaseCount)
                                    .ToArray();

        for (int i = 0; i < m_HowHunted; i++)
        {
            var index = randomIndices[i];
            var newPosition = SpawnList[index].Vector;
            SpawnAgent(Team.Hunted, newPosition);
            iterInd++;
        }
        for (int i = 0; i < m_HowHunter; i++)
        {
            var index = randomIndices[iterInd];
            var newPosition = SpawnList[index].Vector;
            SpawnAgent(Team.Hunter, newPosition);
            iterInd++;
        }
        for (int i = 0; i < m_HowGenerator; i++)
        {
            var index = randomIndices[iterInd];
            var newPosition = SpawnList[index].Vector;
            SpawnGenerator(newPosition);
            iterInd++;
        }
        //Debug.Log("existen " + ActiveObjects.Count + " al inicio del episodio");
    }
}
