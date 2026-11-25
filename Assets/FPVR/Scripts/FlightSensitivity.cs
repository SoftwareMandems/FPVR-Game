using UnityEngine;

namespace FPVR
{
    internal static class FlightSensitivity
    {
        public const int Minimum = 50;
        public const int Default = 100;
        public const int Maximum = 150;
        public const int AxisCount = 4;

        public enum Axis { Pitch, Roll, Yaw, Throttle }

        static readonly string[] keys =
        {
            "FPVR.Sensitivity.Pitch",
            "FPVR.Sensitivity.Roll",
            "FPVR.Sensitivity.Yaw",
            "FPVR.Sensitivity.Throttle"
        };

        static readonly int[] values = new int[AxisCount];
        static bool dirty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Load()
        {
            for (int i = 0; i < AxisCount; i++)
                values[i] = Mathf.Clamp(PlayerPrefs.GetInt(keys[i], Default), Minimum, Maximum);
            dirty = false;
        }

        public static int Get(Axis axis) => values[(int)axis];
        public static float Multiplier(Axis axis) => Get(axis) / (float)Default;

        public static void Set(Axis axis, int percent)
        {
            int index = (int)axis;
            percent = Mathf.Clamp(percent, Minimum, Maximum);
            if (percent == values[index]) return;
            values[index] = percent;
            PlayerPrefs.SetInt(keys[index], percent);
            dirty = true;
        }

        public static void Save()
        {
            if (!dirty) return;
            PlayerPrefs.Save();
            dirty = false;
        }

        public static float ShapeThrottle(float throttle)
        {
            // Shape the response between motor cut, hover and full thrust.
            float sensitivity = Multiplier(Axis.Throttle);
            return sensitivity == 1f ? throttle :
                Mathf.Sign(throttle) * Mathf.Pow(Mathf.Abs(throttle), 1f / sensitivity);
        }
    }
}
