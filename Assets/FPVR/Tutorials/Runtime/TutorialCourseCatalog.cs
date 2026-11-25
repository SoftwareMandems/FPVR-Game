using UnityEngine;

namespace FPVR.Tutorials
{
    public enum TutorialDifficulty { Introductory, Intermediate, Advanced }

    /// <summary>Course configuration; checkpoints exclude the separate finish gate.</summary>
    public sealed class TutorialCourse
    {
        public readonly string Id, Name, Environment, Introduction;
        public readonly TutorialDifficulty Difficulty;
        public readonly Vector3 Start;
        public readonly Quaternion StartRotation;
        public readonly Vector3[] Checkpoints;
        public readonly string[] Instructions;
        public readonly Vector3 Finish;
        public readonly bool FreeFlight;
        public readonly ObstacleCourseLayout Layout;

        public TutorialCourse(string id, string name, string environment, TutorialDifficulty difficulty,
            string introduction, Vector3[] checkpoints, Vector3 finish, string[] instructions, bool freeFlight = false,
            ObstacleCourseLayout layout = null)
        {
            Id = id;
            Name = name;
            Environment = environment;
            Difficulty = difficulty;
            Introduction = introduction;
            Start = new Vector3(0, .16f, 0);
            StartRotation = Quaternion.identity;
            Checkpoints = checkpoints;
            Finish = finish;
            Instructions = instructions;
            FreeFlight = freeFlight;
            Layout = layout;
        }
    }

    /// <summary>
    /// All courses are available immediately. Difficulty describes their relative
    /// arrangement; even the advanced tutorial uses a short, forgiving route.
    /// </summary>
    public static class TutorialCourseCatalog
    {
        public static readonly TutorialCourse[] All =
        {
            new TutorialCourse("free-flight", "Free flight", "Meadow", TutorialDifficulty.Introductory,
                "Practise freely in the open meadow. There are no checkpoints, timer, score or rating. " +
                "Try lifting off, hovering, turning and landing. Pause whenever you need a break.",
                new Vector3[0], Vector3.zero,
                new[] { "Lift off gently. Release the controls to hover, then try moving and turning. No route is required." }, true),

            new TutorialCourse("tutorial-straight", "1. Lift off and fly straight", "Meadow", TutorialDifficulty.Introductory,
                "Learn lift-off and forward flight. Follow two large numbered gates, then the separate FINISH gate. " +
                "Take your time; the route has no time limit.",
                new[] { new Vector3(0, 3, 10), new Vector3(0, 3, 20) }, new Vector3(0, 3, 30),
                new[]
                {
                    "Lift off to about 3 m. Pitch forward gently toward gate 1; release movement to slow down.",
                    "Good! Keep roughly the same height and fly forward through gate 2.",
                    "Both checkpoints are complete. Fly through the separate FINISH gate ahead."
                }),

            new TutorialCourse("tutorial-strafe", "2. Move sideways", "Meadow", TutorialDifficulty.Intermediate,
                "Practise roll: move sideways while still facing forward. Follow the gentle left-right route " +
                "through two checkpoints, then the FINISH gate. The openings stay large.",
                new[] { new Vector3(-4, 3, 12), new Vector3(4, 3, 24) }, new Vector3(0, 3, 36),
                new[]
                {
                    "Lift off to about 3 m. Roll left gently and pitch forward to reach gate 1 on the left.",
                    "Now roll right gently while moving forward toward gate 2. Release movement to slow down.",
                    "Move back toward the centre and fly through FINISH. Small corrections are enough."
                }),

            new TutorialCourse("tutorial-climb-turn", "3. Climb and turn", "Practice Hangar", TutorialDifficulty.Advanced,
                "Combine the controls in a quiet practice hangar. Follow three wide checkpoints with a small " +
                "climb and gentle changes of direction, then the separate FINISH gate.",
                new[] { new Vector3(0, 3, 10), new Vector3(6, 4.5f, 20), new Vector3(6, 3, 31) }, new Vector3(0, 3, 42),
                new[]
                {
                    "Lift off to about 3 m and fly straight through gate 1.",
                    "Climb gently toward 4.5 m. Yaw a little right to face gate 2, then pitch forward.",
                    "Descend gently toward 3 m. Yaw left to face gate 3 farther along the hangar.",
                    "Turn gently toward the FINISH gate on the left and fly through it. You have used all four axes."
                })
        };
    }
}
