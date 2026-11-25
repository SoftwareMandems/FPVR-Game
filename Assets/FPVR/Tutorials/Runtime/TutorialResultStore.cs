using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FPVR.Tutorials
{
    public static class TutorialResultStore
    {
        [Serializable] public sealed class Result
        {
            public string levelId, timestamp, localPlayerId, rating;
            public float totalTime;
            public int checkpoints, crashes, score;
        }
        [Serializable] sealed class Results { public List<Result> results = new List<Result>(); }
        public static string Path => System.IO.Path.Combine(Application.persistentDataPath, "tutorial-results.json");

        // A failed save must never prevent the offline completion review (MIS M4).
        public static bool TrySave(TutorialCourse course, TutorialAttempt attempt, string localPlayerId, out string message)
        {
            try
            {
                var records = File.Exists(Path) ? JsonUtility.FromJson<Results>(File.ReadAllText(Path)) : new Results();
                if (records == null || records.results == null) throw new InvalidDataException("Invalid tutorial history");
                records.results.Add(new Result {
                    levelId = course.Id, timestamp = DateTime.UtcNow.ToString("O"),
                    localPlayerId = localPlayerId, totalTime = attempt.TotalTime,
                    checkpoints = attempt.Passed, crashes = attempt.Crashes, score = attempt.Score, rating = attempt.Rating
                });
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(Path + ".tmp", JsonUtility.ToJson(records, true));
                if (File.Exists(Path)) File.Replace(Path + ".tmp", Path, null);
                else File.Move(Path + ".tmp", Path);
                message = "Saved locally for " + localPlayerId + "."; return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            {
                message = "Local save unavailable. Your result is shown below; you can still retry.";
                Debug.LogWarning("Tutorial result could not be saved: " + e.Message); return false;
            }
        }
    }
}
