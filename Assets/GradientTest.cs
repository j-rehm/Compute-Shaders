using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using static Gradient;

public class GradientTest : MonoBehaviour
{
    public ComputeShader computeShader;

    [Header("Init Settings")]
    [Min(250)]
    public int width = 250;
    [Min(50)]
    public int height = 50;

    [Header("Display Settings")]
    public FilterMode filterMode;

    [SerializeField]
    RenderTexture gradientMap;

    [Header("Gradient Settings")]
    public Gradient gradient;
    private ComputeBuffer colorPointsBuffer;

    void Start()
    {
        CreateRenderTexture(ref gradientMap, width, height, filterMode, GraphicsFormat.R16G16B16A16_SFloat);

        computeShader.SetTexture(0, "GradientMap", gradientMap);
    }

    void Update()
    {
        if (gradient != null && gradient.colors != null)
        {
            CreateBuffer(ref colorPointsBuffer, computeShader, 0, gradient.colors, "colorPointrs");
            CreateStructuredBuffer(ref colorPointsBuffer, gradient.colors);
            computeShader.SetBuffer(0, "colorPoints", colorPointsBuffer);

            computeShader.SetInt("width", width);
            computeShader.SetInt("height", height);

            Dispatch(computeShader, 0, width, height);
        }
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(gradientMap, destination);
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
}
