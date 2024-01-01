using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ComputeShaderTest : MonoBehaviour
{
    public ComputeShader computeShader;
    public RenderTexture renderTexture;

    private void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (renderTexture == null)
        {
            renderTexture = new(256, 256, 24)
            {
                enableRandomWrite = true
            };
            renderTexture.Create();
        }

        computeShader.SetTexture(0, "Result", renderTexture);
        computeShader.SetFloat("Resolution", renderTexture.width);
        computeShader.Dispatch(0, renderTexture.width / 8, renderTexture.height / 8, 1);

        Graphics.Blit(renderTexture, dst);
    }
}
