using UnityEngine;

namespace FPVR
{
    // Real-time quadcopter propeller synthesis, generated sample by sample on the audio thread.
    // Each of the four rotors has its own speed, so when the flight controller speeds some motors up
    // and slows others down to tilt or turn, their pitches split apart and beat against each other
    // the way a real quad does. A rotor's sound is the blade-pass buzz (fundamental + harmonics),
    // broadband air noise chopped by each passing blade, and a faint electrical whine from the motor.
    [RequireComponent(typeof(AudioSource))]
    public class MotorSynth : MonoBehaviour
    {
        public const int Blades = 3;
        const int MotorPolePairs = 7;
        const float TwoPi = Mathf.PI * 2;
        [Range(0, 1)] public float volume = .3f;

        // Written on the main thread, read on the audio thread. Floats are atomic, so no lock is needed.
        readonly float[] target = new float[4];
        readonly float[] rps = new float[4], bladePhase = new float[4], whinePhase = new float[4];
        float sampleRate, gain, hoverRps = 115f;
        float bandLow, bandHigh;
        uint noiseState = 0x9E3779B9u;

        void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;
            // A silent looping clip keeps the source playing; the sound itself is written in OnAudioFilterRead.
            var source = GetComponent<AudioSource>();
            source.clip = AudioClip.Create("Motor Carrier", 1024, 1, (int)sampleRate, false);
            source.loop = true; source.spatialBlend = 0; source.playOnAwake = false;
            source.Play();
        }

        // Rotor speeds in revolutions per second, ordered front-left, front-right, back-left, back-right.
        public void SetRotors(float frontLeft, float frontRight, float backLeft, float backRight, float hover)
        {
            target[0] = frontLeft; target[1] = frontRight; target[2] = backLeft; target[3] = backRight;
            hoverRps = Mathf.Max(1, hover);
        }

        float Noise()
        {
            noiseState ^= noiseState << 13; noiseState ^= noiseState >> 17; noiseState ^= noiseState << 5;
            return (noiseState & 0xFFFFFF) / 8388607.5f - 1;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            float dt = 1f / sampleRate;
            float spool = 1 - Mathf.Exp(-dt * 7f);       // motors can't change speed instantly
            float gainStep = 1 - Mathf.Exp(-dt * 10f);
            float wanted = volume;
            for (int i = 0; i < data.Length; i += channels)
            {
                // Shared band-passed air noise (roughly 300 Hz - 4 kHz), chopped separately by each rotor.
                float white = Noise();
                bandHigh += (white - bandHigh) * .45f;
                bandLow += (bandHigh - bandLow) * .04f;
                float air = bandHigh - bandLow;

                float mix = 0;
                for (int r = 0; r < 4; r++)
                {
                    rps[r] += (target[r] - rps[r]) * spool;
                    float load = rps[r] / hoverRps;
                    if (load < .01f) continue;

                    bladePhase[r] += rps[r] * Blades * dt;
                    if (bladePhase[r] >= 1) bladePhase[r] -= 1;
                    float p = bladePhase[r] * TwoPi;
                    // Harmonics via the Chebyshev recurrence: only one sin and one cos per rotor per sample.
                    float s1 = Mathf.Sin(p), c1 = Mathf.Cos(p), twoC = 2 * c1;
                    float s2 = twoC * s1, s3 = twoC * s2 - s1, s4 = twoC * s3 - s2, s5 = twoC * s4 - s3, s6 = twoC * s5 - s4;
                    float tone = s1 + .7f * s2 + .45f * s3 + .3f * s4 + .2f * s5 + .12f * s6;

                    // Each blade passing gives a short pressure pulse of turbulent air.
                    float chop = .5f + .5f * c1; chop *= chop; chop *= chop;

                    whinePhase[r] += rps[r] * MotorPolePairs * dt;
                    if (whinePhase[r] >= 1) whinePhase[r] -= 1;
                    float whine = Mathf.Sin(whinePhase[r] * TwoPi);

                    // Faster rotors are much louder and get breathier.
                    float loud = load * load;
                    mix += (tone * .16f + air * (.35f + 1.4f * chop) * Mathf.Min(load, 1.6f) + whine * .025f) * loud;
                }
                gain += (wanted - gain) * gainStep;
                float v = mix * gain;
                v /= 1 + Mathf.Abs(v);                    // soft clip
                for (int c = 0; c < channels; c++) data[i + c] = v;
            }
        }
    }
}
