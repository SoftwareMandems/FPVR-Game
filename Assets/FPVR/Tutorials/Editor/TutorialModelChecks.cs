using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FPVR.Tutorials.EditorTools
{
    /// <summary>Run in Edit Mode or with -executeMethod FPVR.Tutorials.EditorTools.TutorialModelChecks.Run.</summary>
    public static class TutorialModelChecks
    {
        [MenuItem("FPVR/Tutorials/Run Model Checks")]
        public static void Run()
        {
            CheckCatalog();
            CheckOrderAndCompletion();
            CheckRecoveryAndTiming();
            CheckScoreBands();
            CheckFreeFlight();
            CheckInvalidData();
            Debug.Log("Tutorial model checks passed: catalog, ordered checkpoints and separate finish, " +
                "crash/respawn/restart, active-flight timing, score boundaries, free flight and invalid data.");
        }

        static void CheckCatalog()
        {
            var ids = new HashSet<string>();
            var environments = new HashSet<string>();
            var difficulties = new HashSet<TutorialDifficulty>();
            int freeAreas = 0;
            foreach (var course in TutorialCourseCatalog.All)
            {
                Require(ids.Add(course.Id), "Course IDs must be unique.");
                Require(!string.IsNullOrWhiteSpace(course.Introduction), "Every course needs an introduction.");
                Require(course.Instructions.Length == course.Checkpoints.Length + 1, "Every segment needs instructions.");
                foreach (var text in course.Instructions) Require(!string.IsNullOrWhiteSpace(text), "Empty instruction.");
                if (course.FreeFlight)
                {
                    freeAreas++;
                    Require(course.Checkpoints.Length == 0, "Free flight cannot contain checkpoints.");
                    continue;
                }
                Require(course.Checkpoints.Length >= 2, "Every course needs at least two checkpoints.");
                environments.Add(course.Environment);
                difficulties.Add(course.Difficulty);
                foreach (var gate in course.Checkpoints)
                    Require(Vector3.Distance(gate, course.Finish) > 1, "Finish must be separate from checkpoints.");
                new TutorialAttempt(course.Checkpoints.Length);
            }
            Require(freeAreas == 1, "Exactly one free-flight area is required.");
            Require(environments.Count >= 2, "Obstacle courses need at least two environments.");
            Require(difficulties.Count == 3, "All three difficulties must be available.");
        }

        internal static void CheckObstacleCatalog()
        {
            var ids = new HashSet<string>();
            foreach (var activity in TutorialCourseCatalog.All) ids.Add(activity.Id);
            var environments = new HashSet<string>();
            var difficulties = new HashSet<TutorialDifficulty>();
            foreach (var course in ObstacleCourseCatalog.All)
            {
                Require(ids.Add(course.Id), "Regular course IDs must be distinct from practice activities.");
                Require(!course.FreeFlight && course.Instructions.Length == 0, "Regular courses cannot be guided practice.");
                Require(course.Checkpoints.Length >= 2 && course.Layout != null, "Every course needs a layout and ordered checkpoints.");
                Require(course.Layout.Opening.x > .6f && course.Layout.Opening.y > .6f, "The drone must fit through the gates.");
                environments.Add(course.Environment);
                difficulties.Add(course.Difficulty);
                var from = course.Start;
                for (int i = 0; i <= course.Checkpoints.Length; i++)
                {
                    var to = i == course.Checkpoints.Length ? course.Finish : course.Checkpoints[i];
                    Require(to.z > from.z + 1 && to.z < 90 && Mathf.Abs(to.x) < 90 && to.y > course.Layout.Opening.y,
                        course.Name + " needs distinct, forward-facing gates above ground and inside flight bounds.");
                    foreach (var obstacle in course.Layout.Obstacles)
                    {
                        var bounds = new Bounds(obstacle.Position, obstacle.Size);
                        bounds.Expand(.7f);
                        var ray = new Ray(from, (to - from).normalized);
                        Require(!bounds.Contains(from) && (!bounds.IntersectRay(ray, out float distance) || distance > Vector3.Distance(from, to)),
                            course.Name + " has an obstacle in the direct flight corridor: " + obstacle.Name);
                    }
                    from = to;
                }
            }
            Require(difficulties.Count == 3 && environments.Count >= 2, "Regular courses need three difficulties across distinct environments.");
        }

        static void CheckOrderAndCompletion()
        {
            var attempt = new TutorialAttempt(2);
            Require(!attempt.PassCheckpoint(0) && !attempt.Finish(), "Guide cannot advance progress.");
            attempt.BeginFlight();
            Require(!attempt.PassCheckpoint(1) && attempt.Passed == 0, "Later gate must not skip the next checkpoint.");
            Require(!attempt.Finish(), "Finish cannot complete before all checkpoints.");
            Require(attempt.PassCheckpoint(0), "First checkpoint should pass.");
            Require(!attempt.PassCheckpoint(0), "A repeated checkpoint cannot advance progress.");
            Require(!attempt.Finish(), "One checkpoint is insufficient.");
            Require(attempt.PassCheckpoint(1), "Second checkpoint should pass.");
            Require(attempt.Phase == TutorialPhase.Flying, "Last checkpoint cannot replace the finish.");
            attempt.Tick(5);
            Require(attempt.Finish(), "Separate finish should complete the course.");
            attempt.Tick(10);
            Require(attempt.TotalTime == 5 && !attempt.Finish(), "Completed time must stop and completion must occur once.");
        }

        static void CheckRecoveryAndTiming()
        {
            var attempt = new TutorialAttempt(2);
            attempt.Tick(8);
            Require(attempt.TotalTime == 0, "Guide time cannot be counted.");
            attempt.BeginFlight();
            attempt.Tick(12);
            attempt.PassCheckpoint(0);
            Require(attempt.TotalTime == 12 && attempt.SectionTime == 0, "Checkpoint resets section, not total.");
            attempt.Tick(3);
            attempt.Pause();
            attempt.Tick(100);
            Require(attempt.TotalTime == 15 && attempt.SectionTime == 3, "Pause time cannot be counted.");
            Require(!attempt.PassCheckpoint(1), "Paused progress must not change.");
            attempt.Resume();
            attempt.Crash();
            attempt.Crash();
            attempt.Tick(100);
            Require(attempt.Crashes == 1 && attempt.TotalTime == 15, "Count each crash once and exclude recovery menu time.");
            Require(!attempt.PassCheckpoint(1), "A crashed drone cannot pass checkpoints.");
            int score = attempt.Score;
            attempt.Respawn();
            Require(attempt.Passed == 1 && attempt.TotalTime == 15 && attempt.SectionTime == 0 &&
                attempt.Crashes == 1 && attempt.Score == score, "Respawn must preserve progress/time/penalties and reset section.");
            attempt.Tick(2);
            Require(attempt.TotalTime == 17 && attempt.SectionTime == 2, "Flight timing resumes after respawn.");
            attempt.Restart();
            Require(attempt.Phase == TutorialPhase.Guide && attempt.Passed == 0 && attempt.TotalTime == 0 &&
                attempt.SectionTime == 0 && attempt.Crashes == 0 && attempt.Score == 1000, "Restart must create a fresh attempt.");
        }

        static void CheckScoreBands()
        {
            CheckScore(200, 800, "Gold");
            CheckScore(201, 799, "Silver");
            CheckScore(400, 600, "Silver");
            CheckScore(401, 599, "Bronze");
            CheckScore(600, 400, "Bronze");
            CheckScore(601, 399, "Needs Improvement");
            CheckScore(1200, 0, "Needs Improvement");
            var attempt = new TutorialAttempt(2);
            attempt.BeginFlight(); attempt.Tick(12.9f); attempt.Crash();
            Require(attempt.Score == 888, "Score must floor flight seconds and subtract 100 per crash.");
        }

        static void CheckScore(float time, int score, string rating)
        {
            var attempt = new TutorialAttempt(2);
            attempt.BeginFlight(); attempt.Tick(time);
            Require(attempt.Score == score && attempt.Rating == rating, "Incorrect score/rating boundary at " + time);
        }

        static void CheckFreeFlight()
        {
            var attempt = new TutorialAttempt(0, true);
            attempt.BeginFlight(); attempt.Tick(123); attempt.Crash(); attempt.Respawn();
            Require(attempt.TotalTime == 0 && attempt.SectionTime == 0 && attempt.Crashes == 0 &&
                attempt.Score == 0 && attempt.Rating == string.Empty, "Free flight must not assess performance.");
            Require(!attempt.PassCheckpoint(0) && !attempt.Finish(), "Free flight has no progress or completion.");
        }

        static void CheckInvalidData()
        {
            Throws(() => new TutorialAttempt(1));
            Throws(() => new TutorialAttempt(2, true));
            var attempt = new TutorialAttempt(2);
            Throws(() => attempt.Tick(-1));
            Throws(() => attempt.Tick(float.NaN));
            Throws(() => attempt.Tick(float.PositiveInfinity));
        }

        static void Throws(Action action)
        {
            try { action(); }
            catch (ArgumentOutOfRangeException) { return; }
            throw new InvalidOperationException("Expected invalid data to be rejected.");
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Tutorial check failed: " + message);
        }
    }
}
