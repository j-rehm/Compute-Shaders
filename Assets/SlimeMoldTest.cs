using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UIElements;

public class SlimeMoldTest : MonoBehaviour
{
    public enum SpawnConfiguration
    {
        CenterOut,
        Random
    }

    private const int updateKernelIndex = 0;
    private const int diffuseKernelIndex = 1;
    private const int gradientKernelIndex = 2;

    public ComputeShader computeShader;

    ComputeBuffer agentBuffer;

    [Header("Init Settings")]
    [Min(250)]
    public int width = 3840;
    [Min(100)]
    public int height = 2160;
    public int agentCount = 1;
    public SpawnConfiguration spawnConfiguration = SpawnConfiguration.CenterOut;

    [Header("Slime Settings")]
    [Min(1)]
    public int stepsPerFrame = 1;
    public float moveSpeed = 10;
    public float turnSpeed = 30;
    public float sensorAngleDegrees = 60;
    public float sensorOffsetDst = 5;
    public int sensorSize = 3;
    public float decayRate = 0.01f;
    public float diffuseRate = 5;

    [Header("Display Settings")]
    public FilterMode filterMode = FilterMode.Point;

    [SerializeField]
    RenderTexture trailMap;
    [SerializeField]
    RenderTexture diffuseTrailMap;
    [SerializeField]
    RenderTexture gradientMap;

    [Header("Gradient Settings")]
    public Gradient gradient;
    private ComputeBuffer colorPointsBuffer;

    void Start()
    {
        CreateRenderTexture(ref trailMap, width, height, filterMode, GraphicsFormat.R16_SFloat);
        CreateRenderTexture(ref diffuseTrailMap, width, height, filterMode, GraphicsFormat.R16_SFloat);
        CreateRenderTexture(ref gradientMap, width, height, filterMode, GraphicsFormat.R16G16B16A16_SFloat);

        computeShader.SetTexture(updateKernelIndex, "TrailMap", trailMap);
        computeShader.SetTexture(diffuseKernelIndex, "TrailMap", trailMap);
        computeShader.SetTexture(diffuseKernelIndex, "DiffuseTrailMap", diffuseTrailMap);
        computeShader.SetTexture(gradientKernelIndex, "GradientMap", gradientMap);
        computeShader.SetTexture(gradientKernelIndex, "TrailMap", trailMap);

        Agent[] agents = new Agent[agentCount];
        Vector2 center = new(width / 2, height / 2);
        for (int i = 0; i < agentCount; i++)
        {
            agents[i] = new Agent()
            {
                position = spawnConfiguration == SpawnConfiguration.CenterOut ? center : new(Random.Range(0, width - 1), Random.Range(0, height - 1)),
                angle = Random.value * Mathf.PI * 2
            };
        }

        CreateBuffer(ref agentBuffer, computeShader, updateKernelIndex, agents, "agents");

        computeShader.SetInt("agentCount", agentCount);
        computeShader.SetInt("width", width);
        computeShader.SetInt("height", height);
    }

    void FixedUpdate()
    {
        for (int i = 0; i < stepsPerFrame; i++)
        {
            computeShader.SetFloat("deltaTime", Time.fixedDeltaTime);
            computeShader.SetFloat("time", Time.fixedTime);

            computeShader.SetFloat("moveSpeed", moveSpeed);
            computeShader.SetFloat("decayRate", decayRate);
            computeShader.SetFloat("diffuseRate", diffuseRate);
            computeShader.SetFloat("turnSpeed", turnSpeed);
            computeShader.SetFloat("sensorAngleDegrees", sensorAngleDegrees);
            computeShader.SetFloat("sensorOffsetDst", sensorOffsetDst);
            computeShader.SetInt("sensorSize", sensorSize);

            Dispatch(computeShader, updateKernelIndex, agentCount);
            Dispatch(computeShader, diffuseKernelIndex, width, height);

            //diffuseTrailMap.
            Graphics.Blit(diffuseTrailMap, trailMap);
        }

        if (gradient != null && gradient.colors != null)
        {
            CreateBuffer(ref colorPointsBuffer, computeShader, gradientKernelIndex, gradient.colors, "colorPointrs");
            CreateStructuredBuffer(ref colorPointsBuffer, gradient.colors);
            computeShader.SetBuffer(gradientKernelIndex, "colorPoints", colorPointsBuffer);

            Dispatch(computeShader, gradientKernelIndex, width, height);
        }
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(gradientMap, destination);
        //Graphics.Blit(diffuseTrailMap, destination);
        //Graphics.Blit(trailMap, destination);
    }

    void OnDestroy()
    {
        if (agentBuffer != null)
            agentBuffer.Release();
    }

    private void CreateRenderTexture(ref RenderTexture renderTexture, int width, int height, FilterMode filterMode, GraphicsFormat format)
    {
        if (renderTexture == null || !renderTexture.IsCreated() || renderTexture.width != width || renderTexture.height != height || renderTexture.graphicsFormat != format)
        {
            if (renderTexture != null)
                renderTexture.Release();

            renderTexture = new(width, height, 0)
            {
                graphicsFormat = format,
                enableRandomWrite = true,
                autoGenerateMips = false
            };
            renderTexture.Create();
        }
        renderTexture.filterMode = filterMode;
    }

    private void CreateBuffer<T>(ref ComputeBuffer computeBuffer, ComputeShader computeShader, int kernelIndex, T[] data, string nameId)
    {
        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
        if (computeBuffer == null || !computeBuffer.IsValid() || computeBuffer.count != data.Length || computeBuffer.stride != stride)
        {
            if (computeBuffer != null)
                computeBuffer.Release();
            computeBuffer = new(data.Length, stride);
            computeBuffer.SetData(data);
            computeShader.SetBuffer(kernelIndex, nameId, computeBuffer);
        }
    }

    public static void CreateStructuredBuffer<T>(ref ComputeBuffer buffer, int count)
    {
        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
        bool createNewBuffer = buffer == null || !buffer.IsValid() || buffer.count != count || buffer.stride != stride;
        if (createNewBuffer)
        {
            if (buffer != null)
                buffer.Release();
            buffer = new ComputeBuffer(count, stride);
        }
    }

    public static void CreateStructuredBuffer<T>(ref ComputeBuffer buffer, T[] data)
    {
        CreateStructuredBuffer<T>(ref buffer, data.Length);
        buffer.SetData(data);
    }

    private void Dispatch(ComputeShader computeShader, int kernelIndex, int numIterationsX, int numIterationsY = 1, int numIterationsZ = 1)
    {
        computeShader.GetKernelThreadGroupSizes(kernelIndex, out uint x, out uint y, out uint z);
        Vector3Int threadGroupSizes = new((int)x, (int)y, (int)z);
        int numGroupsX = Mathf.CeilToInt(numIterationsX / (float)threadGroupSizes.x);
        int numGroupsY = Mathf.CeilToInt(numIterationsY / (float)threadGroupSizes.y);
        int numGroupsZ = Mathf.CeilToInt(numIterationsZ / (float)threadGroupSizes.z);
        computeShader.Dispatch(kernelIndex, numGroupsX, numGroupsY, numGroupsZ);
    }

    public struct Agent
    {
        public Vector2 position;
        public float angle;
    }
}
