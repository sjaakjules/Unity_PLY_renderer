using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointCloud 
{
    public string name;
    public float pointSize = .05f;
    public int pointCount;

    public Vector3[] position, normal;
    public Color32[] colour;

    ParticleSystem.Particle[] particles;
    public PointCloud()
    {

    }
    public void Initialize(List<Vector3> _points, List<Color32> _colours, List<Vector3> _normals)
    {
        pointCount = _points.Count;
        colour = _colours.ToArray();
        position = _points.ToArray();
        normal = _normals.ToArray();
        particles = new ParticleSystem.Particle[pointCount];
    }

    public void setParticles(ParticleSystem ps)
    {
        int rem_Particles = pointCount - ps.particleCount;
        if (rem_Particles < 0) rem_Particles = 0;
        ps.Emit(rem_Particles);
        ps.GetParticles(particles);
        if (particles != null)
        {
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].position = position[i];
                particles[i].startColor = colour[i];
                particles[i].startSize = pointSize;
                particles[i].velocity = Vector3.zero;// normal[i];
                particles[i].rotation3D = normal[i];
            }
            var shape = ps.shape;
            shape.enabled = true;
           // shape.shapeType = ParticleSystemShapeType.Circle;
            shape.alignToDirection = true;
            ps.SetParticles(particles);
        }
        else
        {
            Debug.Log("no particles");
        }
    }

}
