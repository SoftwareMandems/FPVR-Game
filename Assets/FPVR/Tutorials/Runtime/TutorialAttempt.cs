using System;

namespace FPVR.Tutorials
{
    public enum TutorialPhase { Guide, Flying, Paused, Crashed, Complete }

    /// <summary>
    /// Attempt state and evaluation, independent of scene objects and input.
    /// Implements the checkpoint, recovery and active-flight timing rules in the SRS.
    /// </summary>
    public sealed class TutorialAttempt
    {
        public TutorialPhase Phase { get; private set; }
        public int CheckpointCount { get; }
        public bool FreeFlight { get; }
        public int Passed { get; private set; }
        public float TotalTime { get; private set; }
        public float SectionTime { get; private set; }
        public int Crashes { get; private set; }

        // These are the SRS's initial game-balance values, not proficiency measures.
        public int Score => FreeFlight ? 0 : (int)Math.Max(0, 1000.0 - Math.Floor(TotalTime) - 100.0 * Crashes);
        public string Rating => FreeFlight ? string.Empty : Score >= 800 ? "Gold" :
            Score >= 600 ? "Silver" : Score >= 400 ? "Bronze" : "Needs Improvement";

        public TutorialAttempt(int checkpointCount, bool freeFlight = false)
        {
            if ((freeFlight && checkpointCount != 0) || (!freeFlight && checkpointCount < 2))
                throw new ArgumentOutOfRangeException(nameof(checkpointCount),
                    "Free flight has no checkpoints; an obstacle course has at least two.");
            CheckpointCount = checkpointCount;
            FreeFlight = freeFlight;
            Restart();
        }

        public void BeginFlight()
        {
            if (Phase == TutorialPhase.Guide) Phase = TutorialPhase.Flying;
        }

        public void Pause()
        {
            if (Phase == TutorialPhase.Flying) Phase = TutorialPhase.Paused;
        }

        public void Resume()
        {
            if (Phase == TutorialPhase.Paused) Phase = TutorialPhase.Flying;
        }

        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (Phase != TutorialPhase.Flying || FreeFlight) return;
            TotalTime += deltaTime;
            SectionTime += deltaTime;
        }

        public bool PassCheckpoint(int index)
        {
            if (Phase != TutorialPhase.Flying || FreeFlight || index != Passed || Passed >= CheckpointCount)
                return false;
            Passed++;
            SectionTime = 0;
            return true;
        }

        public bool Finish()
        {
            if (Phase != TutorialPhase.Flying || FreeFlight || Passed != CheckpointCount) return false;
            Phase = TutorialPhase.Complete;
            return true;
        }

        public void Crash()
        {
            if (Phase != TutorialPhase.Flying) return;
            if (!FreeFlight) Crashes++;
            Phase = TutorialPhase.Crashed;
        }

        public void Respawn()
        {
            if (Phase != TutorialPhase.Crashed) return;
            SectionTime = 0;
            Phase = TutorialPhase.Flying;
        }

        public void Restart()
        {
            Passed = Crashes = 0;
            TotalTime = SectionTime = 0;
            Phase = TutorialPhase.Guide;
        }
    }
}
