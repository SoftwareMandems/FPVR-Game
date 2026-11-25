using System;
using UnityEngine;
using Obstacle = FPVR.Tutorials.ObstacleCourseLayout.Obstacle;

namespace FPVR.Tutorials
{
    /// <summary>Extra geometry for regular courses, separate from instructional tutorials.</summary>
    public sealed class ObstacleCourseLayout
    {
        // Opening is the unobstructed half-width/half-height, excluding the frame.
        public Vector2 Opening { get; }
        public Obstacle[] Obstacles { get; }

        public ObstacleCourseLayout(Vector2 opening, params Obstacle[] obstacles)
        {
            if (opening.x <= 0 || opening.y <= 0) throw new ArgumentOutOfRangeException(nameof(opening));
            Opening = opening;
            Obstacles = obstacles ?? Array.Empty<Obstacle>();
        }

        public readonly struct Obstacle
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly Vector3 Size;

            public Obstacle(string name, Vector3 position, Vector3 size)
            {
                if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An obstacle needs a name.", nameof(name));
                if (size.x <= 0 || size.y <= 0 || size.z <= 0) throw new ArgumentOutOfRangeException(nameof(size));
                Name = name; Position = position; Size = size;
            }
        }
    }

    /// <summary>
    /// Three immediately selectable regular courses. Gate planes all face +Z;
    /// static hazards sit between gates and clear the continuous centre route.
    /// Regular courses carry descriptions, not tutorial coaching instructions.
    /// </summary>
    public static class ObstacleCourseCatalog
    {
        public static readonly TutorialCourse[] All =
        {
            new TutorialCourse("course-meadow-slalom", "Meadow Slalom", "Meadow", TutorialDifficulty.Introductory,
                "An open-field slalom with five checkpoints, broad openings and alternating field barriers. Finish at the far end of the meadow.",
                new[]
                {
                    new Vector3(0, 3, 12), new Vector3(-4, 3, 24), new Vector3(4, 3, 36),
                    new Vector3(-4, 3, 48), new Vector3(0, 3, 60)
                }, new Vector3(0, 3, 76), Array.Empty<string>(),
                layout: new ObstacleCourseLayout(new Vector2(3.2f, 2.5f),
                    new Obstacle("Right field barrier", new Vector3(4, 2, 18), new Vector3(4, 4, 2)),
                    new Obstacle("Left field barrier", new Vector3(-6, 2, 30), new Vector3(4, 4, 2)),
                    new Obstacle("Outer right barrier", new Vector3(6, 2, 42), new Vector3(4, 4, 2)),
                    new Obstacle("Final left barrier", new Vector3(-6, 2, 54), new Vector3(4, 4, 2)))),

            new TutorialCourse("course-hangar-elevation", "Hangar Elevation", "Practice Hangar", TutorialDifficulty.Intermediate,
                "Seven checkpoints through a hangar route with offset cargo stacks, low barriers and an overhead beam. The route spans several heights.",
                new[]
                {
                    new Vector3(0, 3, 10), new Vector3(-5, 3, 20), new Vector3(-5, 6, 30),
                    new Vector3(5, 6, 40), new Vector3(5, 3, 50), new Vector3(-4, 4.5f, 60),
                    new Vector3(0, 3.5f, 70)
                }, new Vector3(0, 3.5f, 84), Array.Empty<string>(),
                layout: new ObstacleCourseLayout(new Vector2(2.8f, 2.2f),
                    new Obstacle("Entrance cargo stack", new Vector3(3, 2, 15), new Vector3(5, 4, 2)),
                    new Obstacle("Low cargo barrier", new Vector3(-5, 1.5f, 25), new Vector3(9, 3, 2)),
                    new Obstacle("Left cargo stack", new Vector3(-8, 2.5f, 35), new Vector3(5, 5, 2)),
                    new Obstacle("Overhead beam", new Vector3(5, 9, 45), new Vector3(10, 5, 2)),
                    new Obstacle("Right cargo stack", new Vector3(7, 2, 55), new Vector3(5, 4, 2)),
                    new Obstacle("Exit cargo barrier", new Vector3(-2, 1, 65), new Vector3(10, 2, 2)))),

            new TutorialCourse("course-canyon-circuit", "Canyon Circuit", "Canyon", TutorialDifficulty.Advanced,
                "Nine checkpoints across a canyon circuit with alternating rock pillars, low ridges and a high rock bridge. A separate finish closes the route.",
                new[]
                {
                    new Vector3(0, 4, 10), new Vector3(-6, 4, 18), new Vector3(-8, 6, 26),
                    new Vector3(-2, 7, 34), new Vector3(6, 7, 42), new Vector3(8, 4.5f, 50),
                    new Vector3(2, 3.5f, 58), new Vector3(-5, 5, 66), new Vector3(0, 5.5f, 74)
                }, new Vector3(0, 4, 86), Array.Empty<string>(),
                layout: new ObstacleCourseLayout(new Vector2(2.3f, 1.8f),
                    new Obstacle("Entrance rock pillar", new Vector3(4, 2.5f, 14), new Vector3(5, 5, 2)),
                    new Obstacle("First low ridge", new Vector3(-7, 1.75f, 22), new Vector3(9, 3.5f, 2)),
                    new Obstacle("Left rock pillar", new Vector3(-10, 3, 30), new Vector3(5, 6, 2)),
                    new Obstacle("Central ridge", new Vector3(2, 2.25f, 38), new Vector3(10, 4.5f, 2)),
                    new Obstacle("High rock bridge", new Vector3(8, 10, 46), new Vector3(12, 5, 2)),
                    new Obstacle("Right rock pillar", new Vector3(11, 2.5f, 54), new Vector3(6, 5, 2)),
                    new Obstacle("Return ridge", new Vector3(-1.5f, 1.25f, 62), new Vector3(10, 2.5f, 2)),
                    new Obstacle("Return rock pillar", new Vector3(-10, 3, 70), new Vector3(5, 6, 2)),
                    new Obstacle("Finish rock pillar", new Vector3(7, 2, 80), new Vector3(4, 4, 2))))
        };
    }
}
