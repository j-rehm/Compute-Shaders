using System;
using UnityEngine;

public class RandomGpuTest : MonoBehaviour
{
    public ComputeShader computeShader;
    public RenderTexture renderTexture;

    public int width;
    public int height;

    //public void OnStart()
    //{
    //    renderTexture = new(width, height, 24)
    //    {
    //        enableRandomWrite = true
    //    };
    //    renderTexture.Create();
    //}

    private void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (renderTexture == null)
        {
            renderTexture = new(width, height, 24)
            {
                enableRandomWrite = true
            };
            renderTexture.Create();
        }

        computeShader.SetTexture(0, "Texture", renderTexture);
        computeShader.SetInt("width", width);
        computeShader.SetInt("height", height);
        computeShader.Dispatch(0, width / 8, height / 8, 1);

        Graphics.Blit(renderTexture, dst);
    }
}
