using UnityEngine;

namespace FPVR
{
    // All sounds are synthesised in code, so there are no audio files to import:
    // propellers that follow the flight physics (see MotorSynth), wind that grows with speed,
    // a whoosh when you punch it, coin sounds for each gate and a cash-register jackpot at the finish.
    [RequireComponent(typeof(DroneFlight))]
    public class DroneAudio : MonoBehaviour
    {
        [Range(0, 1)] public float motorVolume = .3f, windVolume = .35f, effectsVolume = .8f;
        [Tooltip("Propeller speed at hover in revolutions per second. Higher sounds like a smaller, zippier drone.")]
        public float hoverRps = 115f;
        [Tooltip("How much each motor speeds up or slows down when the drone tilts or turns.")]
        public float motorDifferential = .012f;
        const int Rate = 44100;
        const float TwoPi = Mathf.PI * 2;
        DroneFlight drone;
        MotorSynth motors;
        AudioSource wind, effects;
        AudioClip coin, jackpot, whoosh;
        float nextWhoosh;
        bool wasPushing;

        void Awake()
        {
            drone = GetComponent<DroneFlight>();
            // The synth needs its own object: an audio filter applies to the AudioSource beside it.
            var motorObject = new GameObject("Motor Sound");
            motorObject.transform.SetParent(transform, false);
            motorObject.AddComponent<AudioSource>();
            motors = motorObject.AddComponent<MotorSynth>();
            wind = Source(WindLoop());
            effects = Source(null);
            coin = Coin(); jackpot = Jackpot(); whoosh = Whoosh();
        }

        AudioSource Source(AudioClip loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false; s.spatialBlend = 0; s.volume = loop ? 0 : 1;
            if (loop) { s.clip = loop; s.loop = true; s.Play(); }
            return s;
        }

        void Update()
        {
            if (!motors || !wind || !effects) return;   // e.g. scripts were recompiled during Play mode
            bool flying = drone.armed && !drone.crashed;
            float climb = drone.Climb, push = drone.Tilt.magnitude;
            UpdateMotors(flying);

            float speed = drone.Body.linearVelocity.magnitude;
            float gust = Mathf.Clamp01(speed / 12f);
            wind.volume = Mathf.Lerp(wind.volume, windVolume * gust * gust, 1 - Mathf.Exp(-4 * Time.deltaTime));
            wind.pitch = .8f + gust * .6f;

            bool pushing = flying && (climb > .6f || push > .7f);
            if (pushing && !wasPushing && Time.time > nextWhoosh)
            {
                effects.pitch = 1;
                effects.PlayOneShot(whoosh, effectsVolume * .45f);
                nextWhoosh = Time.time + .9f;
            }
            wasPushing = pushing;
        }

        // Thrust goes with the square of propeller speed, so speed follows the square root of thrust.
        // On top of that, the flight controller's torque speeds up the motors on one side to tilt the
        // drone (rear pair to pitch forward, left pair to roll right, one diagonal pair to yaw).
        void UpdateMotors(bool flying)
        {
            motors.volume = motorVolume;
            if (!flying) { motors.SetRotors(0, 0, 0, 0, hoverRps); return; }
            float rps = hoverRps * Mathf.Sqrt(Mathf.Max(drone.Thrust, .12f));
            Vector3 t = drone.LocalTorque * motorDifferential;
            float pitch = t.x, roll = t.z, yaw = t.y;
            float time = Time.time;
            float Rotor(float mix, int i) =>
                rps * (1 + Mathf.Clamp(mix, -.15f, .15f) + (Mathf.PerlinNoise(time * 2.5f, i * 7.3f) - .5f) * .012f);
            motors.SetRotors(
                Rotor(-pitch - roll + yaw, 0),   // front-left
                Rotor(-pitch + roll - yaw, 1),   // front-right
                Rotor(pitch - roll - yaw, 2),    // back-left
                Rotor(pitch + roll + yaw, 3),    // back-right
                hoverRps);
        }

        public void PlayCoin(int gatesPassed)
        {
            // Each gate pitches up a little, so a streak sounds like it's building.
            effects.pitch = 1 + Mathf.Min(gatesPassed, 10) * .04f;
            effects.PlayOneShot(coin, effectsVolume);
        }

        public void PlayJackpot()
        {
            effects.pitch = 1;
            effects.PlayOneShot(jackpot, effectsVolume);
        }

        // ---------- Synthesis ----------

        static AudioClip Clip(string name, float[] data)
        {
            float peak = .0001f;
            foreach (float v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < data.Length; i++) data[i] *= .85f / peak;
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip WindLoop()
        {
            var d = new float[Rate * 2];
            var rng = new System.Random(9); float a = 0, b = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                a += ((float)rng.NextDouble() * 2 - 1 - a) * .06f;
                b += (a - b) * .06f;
                d[i] = b * (.75f + .25f * Mathf.Sin(TwoPi * .5f * t));
            }
            return Clip("Wind", d);
        }

        // Classic two-note coin pickup with a little bell sparkle on top.
        static AudioClip Coin()
        {
            var d = new float[(int)(Rate * .55f)];
            float phase = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float f = t < .075f ? 988 : 1319;
                phase += TwoPi * f / Rate;
                float env = (t < .075f ? 1 : Mathf.Exp(-(t - .075f) * 7)) * Mathf.Clamp01(t / .004f);
                float tone = Mathf.Sin(phase) + Mathf.Sin(phase * 3) / 3 + Mathf.Sin(phase * 5) / 5;
                float sparkle = t < .075f ? 0 : Mathf.Sin(TwoPi * 2637 * t) * Mathf.Exp(-(t - .075f) * 10) * .4f;
                d[i] = tone * env * .5f + sparkle;
            }
            return Clip("Coin", d);
        }

        // "Ka-ching": register drawer clack, a ringing bell, and a run of coins.
        static AudioClip Jackpot()
        {
            var d = new float[(int)(Rate * 1.8f)];
            var rng = new System.Random(21); float prev = 0;
            float[] coins = { 1319, 1568, 1976, 2637, 3136 };
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate, s = 0;
                if (t < .09f)
                {
                    float noise = (float)rng.NextDouble() * 2 - 1;
                    s += (noise - prev) * Mathf.Exp(-t * 45) * .9f; prev = noise;
                }
                float bt = t - .08f;
                if (bt > 0)
                {
                    float ring = Mathf.Exp(-bt * 2.2f) * Mathf.Clamp01(bt / .003f);
                    s += (Mathf.Sin(TwoPi * 1568 * bt) + .6f * Mathf.Sin(TwoPi * 4327 * bt) * Mathf.Exp(-bt * 3)
                        + .35f * Mathf.Sin(TwoPi * 8467 * bt) * Mathf.Exp(-bt * 6)) * ring * .6f;
                }
                for (int c = 0; c < coins.Length; c++)
                {
                    float ct = t - .18f - c * .075f;
                    if (ct > 0 && ct < .4f)
                    {
                        float ph = TwoPi * coins[c] * ct;
                        s += (Mathf.Sin(ph) + Mathf.Sin(ph * 3) / 3) * Mathf.Exp(-ct * 9) * Mathf.Clamp01(ct / .003f) * .35f;
                    }
                }
                d[i] = s;
            }
            return Clip("Jackpot", d);
        }

        // Rising air rush: noise that opens up and swells, with a faint upward tone sweep.
        static AudioClip Whoosh()
        {
            var d = new float[(int)(Rate * .6f)];
            var rng = new System.Random(5); float lp = 0, phase = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate, p = t / .6f;
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(p, .6f));
                lp += ((float)rng.NextDouble() * 2 - 1 - lp) * Mathf.Lerp(.04f, .35f, p);
                phase += TwoPi * Mathf.Lerp(180, 520, p) / Rate;
                d[i] = (lp * .9f + Mathf.Sin(phase) * .12f) * env;
            }
            return Clip("Whoosh", d);
        }
    }
}
