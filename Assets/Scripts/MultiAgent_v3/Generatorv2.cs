using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class Generatorv2 : MonoBehaviour
{
    public float DespawnTime;
    private float m_GenerateTime;
    public float GrowthTime;

    public float SpawnRate;
    public int SpawnCount;

    // detection fertilizer
    public float detectionRadius = 20f;

    [SerializeField] private LayerMask FertilizerLayer;
    [SerializeField] private LayerMask CarriorLayer;

    // Array de un solo collider
    private Collider[] colliderBuffer = new Collider[1];


    private EnvControllerv3 m_EnvController;

    [SerializeField] private GameObject m_Plant;

    private Material m_PreMaterial;
    public Material PostMaterial;
    private Renderer m_Renderer;

    private TimerManager m_TimeManager;



    private void Awake()
    {
        m_EnvController = GetComponentInParent<EnvControllerv3>();
        m_TimeManager = GetComponentInParent<TimerManager>();
        m_Renderer = m_Plant.GetComponent<Renderer>();
        m_PreMaterial = m_Renderer.material;
        m_EnvController.InitializeGenerator(this);
        m_GenerateTime = (DespawnTime - GrowthTime -1) / SpawnCount;
    }
    private void OnEnable()
    {
        // Registro de eventos
        StartCoroutine(ScaleOverTime(m_Plant.transform, Vector3.one, GrowthTime));

        //timeManager.RegisterTimerEvent(GrowthTime, PlantGrows);
        InitializeEvents();
    }
    private void OnDisable()
    {
        m_Plant.transform.transform.localScale = new Vector3(0.2f,0.2f,0.2f);
        m_Renderer.material = m_PreMaterial;
    }
    private void InitializeEvents()
    {
        float countGenerate = 0;
        for (int i = 0; i < SpawnCount; i++)
        {
            countGenerate += m_GenerateTime;
            m_TimeManager.RegisterTimerEvent(countGenerate, GenerateFood);
        }
        m_TimeManager.RegisterTimerEvent(DespawnTime, DetectFertilizer);
    }
    private void GenerateFood()
    {
        float ran = Random.value;
        if (ran < SpawnRate)
        {
            m_EnvController.SpawnTarget(transform.position);
        }
    }
    private void DetectFertilizer()
    {
        // Usamos OverlapSphereNonAlloc para llenar el array con hasta 1 collider
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, colliderBuffer, FertilizerLayer | CarriorLayer);

        if (hitCount > 0)
        {
            GameObject detectedObject = colliderBuffer[0].gameObject;

            if ((FertilizerLayer.value & (1 << detectedObject.layer)) > 0)
            {
                m_EnvController.DespawnFertilizer(detectedObject);
                InitializeEvents();
                return;
            }
            else if ((CarriorLayer.value & (1 << detectedObject.layer)) > 0)
            {
                m_EnvController.DespawnCarrion(detectedObject);
            }

        }
        //m_Renderer.material = m_PreMaterial;
        //m_Plant.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
        m_EnvController.DespawnGenerator(gameObject);
    }

    IEnumerator ScaleOverTime(Transform target, Vector3 endScale, float time)
    {
        Vector3 startScale = target.transform.localScale;
        float elapsedTime = 0f;
        while (elapsedTime < time)
        {
            target.transform.localScale = Vector3.Lerp(startScale, endScale, elapsedTime / time);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        target.transform.localScale = endScale;
        m_Renderer.material = PostMaterial;
    }
    // es necesario un reset para borrar todas las corrutinas cuando se resetea la escena ///////////////
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(new Vector3(transform.position.x, transform.position.y, transform.position.z), detectionRadius);
    }
}
