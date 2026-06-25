using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class ParticleGatherToCenter : MonoBehaviour
{
    public float gatherDuration = 0.6f;
    public AnimationCurve gatherCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;
    private Vector3[] startPositions;
    private float timer;
    private bool playing;

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        particles = new ParticleSystem.Particle[ps.main.maxParticles];
    }

    public void Play()
    {
        timer = 0f;
        playing = true;

        ps.Play();

        int count = ps.GetParticles(particles);
        startPositions = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            startPositions[i] = particles[i].position;
        }
    }

    void LateUpdate()
    {
        if (!playing) return;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / gatherDuration);
        float curvedT = gatherCurve.Evaluate(t);

        int count = ps.GetParticles(particles);

        for (int i = 0; i < count; i++)
        {
            if (i < startPositions.Length)
            {
                particles[i].position = Vector3.Lerp(startPositions[i], Vector3.zero, curvedT);
            }
        }

        ps.SetParticles(particles, count);

        if (t >= 1f)
        {
            playing = false;
        }
    }
}
