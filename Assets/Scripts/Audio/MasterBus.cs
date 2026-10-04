using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>
    /// The final stage of the mix, on the audio listener: a gentle bus compressor that glues music,
    /// ambience and foley together, then a peak limiter with a soft ceiling so stacked one-shots
    /// (the trunk slam, confetti and horn together) can never clip the output.
    /// Runs on the audio thread and allocates nothing.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    public class MasterBus : MonoBehaviour
    {
        // +4 dB make-up into the bus: the raw mix sits around -25 LUFS, quiet next to other PC games.
        const float Makeup = 1.58f;
        // Glue: 2:1 above -10 dBFS, slow enough to keep transients.
        const float GlueThreshold = 0.32f;
        const float GlueRatio = 2f;
        // Limiter ceiling (-1.5 dBFS); a soft clip above it is only a safety net.
        const float Ceiling = 0.84f;
        const float Knee = 0.9f;
        const float Max = 0.98f;

        float glueEnv, limitGain = 1f;
        float glueAttack, glueRelease, limitRelease;
        volatile float reduction;

        /// <summary>Current limiter gain reduction in dB (for diagnostics).</summary>
        public float ReductionDb => reduction;

        // Session statistics for the self-test: loudest input and output sample, how many input
        // samples would have clipped without the bus, and the deepest gain reduction.
        static float peakIn, peakOut, deepest;
        static long overs, samples;

        public static string Report() =>
            $"[Audio] mix peak {Db(peakIn):0.0} dBFS in, {Db(peakOut):0.0} dBFS out; {overs} of {samples} samples over 0 dBFS before the limiter; deepest gain reduction {Db(deepest):0.0} dB";

        static float Db(float x) => 20f * Mathf.Log10(Mathf.Max(x, 0.00001f));

        void Awake()
        {
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            glueAttack = Coefficient(0.01f, rate);
            glueRelease = Coefficient(0.25f, rate);
            limitRelease = Coefficient(0.12f, rate);
        }

        static float Coefficient(float seconds, int rate) => Mathf.Exp(-1f / (seconds * rate));

        void OnAudioFilterRead(float[] data, int channels)
        {
            float worst = 1f, inPeak = 0f, outPeak = 0f;
            long overCount = 0;
            for (int i = 0; i < data.Length; i += channels)
            {
                // Linked stereo detector.
                float peak = 0f;
                for (int c = 0; c < channels; c++)
                {
                    data[i + c] *= Makeup;
                    float a = data[i + c] < 0f ? -data[i + c] : data[i + c];
                    if (a > peak) peak = a;
                }
                if (peak > inPeak) inPeak = peak;
                if (peak > 1f) overCount++;

                // Bus compressor (smoothed envelope, gain computed in the linear domain).
                glueEnv = peak > glueEnv ? glueAttack * (glueEnv - peak) + peak : glueRelease * (glueEnv - peak) + peak;
                float glue = 1f;
                if (glueEnv > GlueThreshold)
                {
                    float over = glueEnv / GlueThreshold;
                    glue = Mathf.Pow(over, 1f / GlueRatio - 1f);
                }

                // Limiter: instant attack, smooth release.
                float level = peak * glue;
                float want = level > Ceiling ? Ceiling / level : 1f;
                limitGain = want < limitGain ? want : limitRelease * (limitGain - want) + want;
                float gain = glue * limitGain;
                if (gain < worst) worst = gain;

                for (int c = 0; c < channels; c++)
                {
                    float s = data[i + c] * gain;
                    // Never reached unless something upstream misbehaves; then bend rather than clip.
                    float a = s < 0f ? -s : s;
                    if (a > Knee)
                    {
                        float x = (a - Knee) / (Max - Knee);
                        a = Knee + (Max - Knee) * (x / (1f + x));
                        s = s < 0f ? -a : a;
                    }
                    data[i + c] = s;
                    if (a > outPeak) outPeak = a;
                }
            }
            reduction = 20f * Mathf.Log10(Mathf.Max(worst, 0.0001f));
            if (inPeak > peakIn) peakIn = inPeak;
            if (outPeak > peakOut) peakOut = outPeak;
            if (worst < deepest || samples == 0) deepest = worst;
            overs += overCount;
            samples += data.Length / channels;
        }
    }
}
