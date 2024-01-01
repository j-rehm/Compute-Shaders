using System;
using UnityEngine;

[CreateAssetMenu()]
public class Gradient : ScriptableObject
{
    public ColorPoint[] colors;
    [Serializable]
    public struct ColorPoint
    {
        public float position;
        public Color color;
    }
}
