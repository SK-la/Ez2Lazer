// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public static class ManiaModHelper
    {
        public static readonly int[] DIVIDE_NUMBER = [2, 4, 8, 3, 6, 9, 5, 7, 12, 16, 48, 35, 64];

        public static void AddOriginalNoteByColumn(List<ManiaHitObject> newObjects, IGrouping<int, ManiaHitObject> column)
        {
            var newColumnObjects = new List<ManiaHitObject>();
            var locations = column.OfType<Note>().Select(n => (startTime: n.StartTime, endTime: n.StartTime, samples: n.Samples))
                                  .Concat(column.OfType<HoldNote>().SelectMany(h => new[]
                                  {
                                      (startTime: h.StartTime, endTime: h.EndTime, samples: h.GetNodeSamples(0))
                                      //(startTime: h.EndTime, samples: h.GetNodeSamples(1))
                                  }))
                                  .OrderBy(h => h.startTime).ToList();

            for (int i = 0; i < locations.Count; i++)
            {
                if (locations[i].startTime != locations[i].endTime)
                {
                    newColumnObjects.Add(new HoldNote
                    {
                        Column = column.Key,
                        StartTime = locations[i].startTime,
                        EndTime = locations[i].endTime,
                        NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                    });
                }
                else
                {
                    newColumnObjects.Add(new Note
                    {
                        Column = column.Key,
                        StartTime = locations[i].startTime,
                        Samples = locations[i].samples
                    });
                }
            }

            newObjects.AddRange(newColumnObjects);
        }

        [Obsolete]
        public static void Transform(Random rng,
                                     double mu,
                                     double sigmaDivisor,
                                     int divide,
                                     int percentage,
                                     double error,
                                     bool originalLN,
                                     IBeatmap beatmap,
                                     List<ManiaHitObject> newObjects,
                                     List<ManiaHitObject> oldObjects,
                                     int gap = -1,
                                     int forTransformColumnNum = 0,
                                     int divide2 = -1,
                                     double mu2 = -2,
                                     double mu1Dmu2 = -1)
        {
            var locations = oldObjects.OfType<Note>().Select(n => (column: n.Column, startTime: n.StartTime, endTime: n.StartTime, samples: n.Samples))
                                      .Concat(oldObjects.OfType<HoldNote>().SelectMany(h => new[]
                                      {
                                          (column: h.Column, startTime: h.StartTime, endTime: h.EndTime, samples: h.GetNodeSamples(0))
                                      }))
                                      .OrderBy(h => h.startTime).ToList();
            var maniaBeatmap = (ManiaBeatmap)beatmap;
            int keys = maniaBeatmap.TotalColumns;
            int maxGap = gap;
            var randomColumnList = SelectRandom(Enumerable.Range(0, keys), rng, forTransformColumnNum == 0 ? keys : forTransformColumnNum).ToList();
            var noteList = new List<(double lastStartTime, double lastEndTime, bool lastLN, double thisStartTime, double thisEndTime, bool thisLN)>(keys);
            noteList = Enumerable.Repeat((double.NaN, double.NaN, false, double.NaN, double.NaN, false), keys).ToList();
            var sampleList = new List<(IList<HitSampleInfo> lastSample, IList<HitSampleInfo> thisSample)>(keys);

            foreach (var timeGroup in locations.GroupBy(h => h.startTime))
            {
                foreach (var note in timeGroup)
                {
                    if (randomColumnList.Contains(note.column))
                    {
                        if (double.IsNaN(noteList[note.column].thisStartTime))
                        {
                            noteList[note.column] = (double.NaN, double.NaN, false, note.startTime, note.endTime, note.startTime != note.endTime);
                            sampleList[note.column] = (sampleList[note.column].thisSample, note.samples);
                        }
                        else
                        {
                            noteList[note.column] = (noteList[note.column].thisStartTime, noteList[note.column].thisEndTime, noteList[note.column].thisLN, note.startTime, note.endTime,
                                note.startTime != note.endTime);
                            sampleList[note.column] = (sampleList[note.column].thisSample, note.samples);

                            double fullDuration = noteList[note.column].thisStartTime - noteList[note.column].lastStartTime;

                            double duration = GetDurationByDistribution(rng, beatmap, noteList[note.column].lastStartTime, fullDuration, mu, sigmaDivisor, divide, error, divide2, mu2, mu1Dmu2);

                            JudgementToNote(rng, newObjects, noteList[note.column].lastStartTime, note.column, noteList[note.column].lastEndTime, sampleList[note.column].lastSample, originalLN,
                                noteList[note.column].lastLN, percentage, duration);
                        }
                    }
                    else
                    {
                        if (note.startTime != note.endTime && originalLN)
                            newObjects.AddNote(note.samples, note.column, note.startTime, note.endTime);
                        else
                            newObjects.AddNote(note.samples, note.column, note.startTime);
                    }
                }

                gap--;

                if (gap == 0)
                {
                    randomColumnList = SelectRandom(Enumerable.Range(0, keys), rng, forTransformColumnNum).ToList();
                    gap = maxGap;
                }
            }

            for (int i = 0; i < keys; i++)
            {
                if (!double.IsNaN(noteList[i].lastStartTime))
                {
                    double fullDuration = noteList[i].thisStartTime - noteList[i].lastStartTime;

                    double duration = GetDurationByDistribution(rng, beatmap, noteList[i].lastStartTime, fullDuration, mu, sigmaDivisor, divide, error, divide2, mu2, mu1Dmu2);

                    JudgementToNote(rng, newObjects, noteList[i].lastStartTime, i, noteList[i].lastEndTime, sampleList[i].lastSample, originalLN, noteList[i].lastLN, percentage, duration);
                }

                if (!double.IsNaN(noteList[i].thisStartTime))
                {
                    if (rng.Next(100) >= percentage || Math.Abs(noteList[i].thisEndTime - noteList[i].thisStartTime) <= error)
                        newObjects.AddNote(sampleList[i].thisSample, i, noteList[i].thisStartTime);
                    else
                        newObjects.AddNote(sampleList[i].thisSample, i, noteList[i].thisStartTime, noteList[i].thisEndTime);
                }
            }
        }

        public static void JudgementToNote(Random rng,
                                           List<ManiaHitObject> newObjects,
                                           double startTime,
                                           int column,
                                           double endTime,
                                           IList<HitSampleInfo> samples,
                                           bool originalLN,
                                           bool isLN,
                                           int percentage,
                                           double duration)
        {
            if (originalLN && isLN)
                newObjects.AddNote(samples, column, startTime, endTime);
            else if (rng.Next(100) < percentage && !double.IsNaN(duration))
                newObjects.AddLNByDuration(samples, column, startTime, duration);
            else
                newObjects.AddNote(samples, column, startTime);
        }

        /// <summary>
        /// Return original LN objects.
        /// </summary>
        /// <param name="rng"></param>
        /// <param name="mu"></param>
        /// <param name="sigmaDivisor"></param>
        /// <param name="divide"></param>
        /// <param name="percentage"></param>
        /// <param name="error"></param>
        /// <param name="originalLN"></param>
        /// <param name="beatmap"></param>
        /// <param name="newObjects"></param>
        /// <param name="column"></param>
        /// <param name="divide2"></param>
        /// <param name="mu2"></param>
        /// <param name="mu1Dmu2"></param>
        /// <returns></returns>
        public static List<ManiaHitObject> Transform(Random rng,
                                                     double mu,
                                                     double sigmaDivisor,
                                                     int divide,
                                                     int percentage,
                                                     double error,
                                                     bool originalLN,
                                                     IBeatmap beatmap,
                                                     List<ManiaHitObject> newObjects,
                                                     IGrouping<int, ManiaHitObject> column,
                                                     int divide2 = -1,
                                                     double mu2 = -2,
                                                     double mu1Dmu2 = -1)
        {
            var originalLNObjects = new List<ManiaHitObject>();
            var newColumnObjects = new List<ManiaHitObject>();
            var locations = column.OfType<Note>().Select(n => (startTime: n.StartTime, samples: n.Samples, endTime: n.StartTime))
                                  .Concat(column.OfType<HoldNote>().SelectMany(h => new[]
                                  {
                                      (startTime: h.StartTime, samples: h.GetNodeSamples(0), endTime: h.EndTime)
                                  }))
                                  .OrderBy(h => h.startTime).ToList();

            for (int i = 0; i < locations.Count - 1; i++)
            {
                // double offset = locations[0].startTime;
                double fullDuration = locations[i + 1].startTime - locations[i].startTime; // Full duration of the hold note.
                double duration = GetDurationByDistribution(rng, beatmap, locations[i].startTime, fullDuration, mu, sigmaDivisor, divide, error, divide2, mu2, mu1Dmu2);

                // Try to make timing point more precision.
                // double beatLength = beatmap.ControlPointInfo.TimingPointAt(locations[i].startTime).BeatLength;
                // double endTime = PreciseTime(locations[i].startTime + duration, beatLength, offset, error);

                if (originalLN && locations[i].startTime != locations[i].endTime)
                {
                    newColumnObjects.AddNote(locations[i].samples, column.Key, locations[i].startTime, locations[i].endTime);
                    originalLNObjects.AddNote(locations[i].samples, column.Key, locations[i].startTime, locations[i].endTime);
                }
                else if (rng.Next(100) < percentage && !double.IsNaN(duration))
                    newColumnObjects.AddLNByDuration(locations[i].samples, column.Key, locations[i].startTime, duration);
                else
                    newColumnObjects.AddNote(locations[i].samples, column.Key, locations[i].startTime);
            }

            // Dispose last note on the column

            if (Math.Abs(locations[locations.Count - 1].startTime - locations[locations.Count - 1].endTime) <= error || rng.Next(100) >= percentage)
            {
                newColumnObjects.Add(new Note
                {
                    Column = column.Key,
                    StartTime = locations[locations.Count - 1].startTime,
                    Samples = locations[locations.Count - 1].samples
                });
            }
            else
            {
                newColumnObjects.Add(new HoldNote
                {
                    Column = column.Key,
                    StartTime = locations[locations.Count - 1].startTime,
                    Duration = locations[locations.Count - 1].endTime - locations[locations.Count - 1].startTime,
                    NodeSamples = [locations[locations.Count - 1].samples, Array.Empty<HitSampleInfo>()]
                });
            }

            newObjects.AddRange(newColumnObjects);

            return originalLNObjects;
        }

        public static double GetDurationByDistribution(Random rng,
                                                       IBeatmap beatmap,
                                                       double startTime,
                                                       double limitDuration,
                                                       double mu,
                                                       double sigmaDivisor,
                                                       int divide,
                                                       double error,
                                                       int divide2 = -1,
                                                       double mu2 = -2,
                                                       double mu1Dmu2 = -1)
        {
            // Beat length at the end of the hold note.
            double beatLength = beatmap.ControlPointInfo.TimingPointAt(startTime).BeatLength;
            // double beatBPM = beatmap.ControlPointInfo.TimingPointAt(startTime).BPM;
            double timeDivide = beatLength / divide; //beatBPM / 60 * 100 / Divide.Value;
            bool flag = true; // Can be transformed to LN
            double sigma = timeDivide / sigmaDivisor; // LN duration σ
            int timenum = (int)Math.Round(limitDuration / timeDivide, 0);
            double duration = TimeRound(timeDivide, RandDistribution(rng, limitDuration * mu / 100, sigma));

            if (mu1Dmu2 != -1)
            {
                if (rng.Next(100) >= mu1Dmu2)
                {
                    timeDivide = beatLength / divide2;
                    sigma = timeDivide / sigmaDivisor;
                    timenum = (int)Math.Round(limitDuration / timeDivide, 0);
                    duration = TimeRound(timeDivide, RandDistribution(rng, limitDuration * mu2 / 100, sigma));
                }
            }

            if (mu == -1)
            {
                if (timenum < 1)
                    duration = timeDivide;
                else
                {
                    int rdtime = rng.Next(1, timenum);
                    duration = rdtime * timeDivide;
                    duration = TimeRound(timeDivide, duration);
                }
            }

            if (duration > limitDuration - timeDivide)
            {
                duration = limitDuration - timeDivide;
                duration = TimeRound(timeDivide, duration);
            }

            if (duration <= timeDivide) duration = timeDivide;

            if (duration >= limitDuration - error) // Additional processing.
                flag = false;

            return flag ? duration : double.NaN;
        }

        public static void AfterTransform(List<ManiaHitObject> afterObjects,
                                          List<ManiaHitObject> originalLNObjects,
                                          IBeatmap beatmap,
                                          Random rng,
                                          bool originalLN,
                                          int gap = -1,
                                          int transformColumnNum = 0,
                                          double limitDuration = 0,
                                          int lineSpacing = 0,
                                          bool invertSpacing = false)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;
            var resultObjects = new List<ManiaHitObject>();
            var originalLNSet = new HashSet<ManiaHitObject>(originalLNObjects);
            int keys = maniaBeatmap.TotalColumns;
            if (transformColumnNum > keys) transformColumnNum = keys;
            var randomColumnSet = SelectRandom(Enumerable.Range(0, keys), rng, transformColumnNum == 0 ? keys : transformColumnNum).ToHashSet();

            int maxGap = gap;

            foreach (var timeGroup in afterObjects.GroupBy(h => h.StartTime))
            {
                foreach (var note in timeGroup)
                {
                    if (originalLNSet.Contains(note) && originalLN)
                        resultObjects.Add(note);
                    else if (randomColumnSet.Contains(note.Column) && note.StartTime != note.GetEndTime()
                                                                   && (limitDuration > 0 && note.GetEndTime() - note.StartTime <= limitDuration * 1000 || limitDuration == 0))
                        resultObjects.Add(note);
                    else
                        resultObjects.AddNote(note.Samples, note.Column, note.StartTime);
                }

                gap--;

                if (gap == 0)
                {
                    randomColumnSet = SelectRandom(Enumerable.Range(0, keys), rng, transformColumnNum).ToHashSet();
                    gap = maxGap;
                }
            }

            int maxSpacing = lineSpacing;

            if (maxSpacing > 0)
            {
                afterObjects = resultObjects.OrderBy(h => h.StartTime).ToList();
                resultObjects = new List<ManiaHitObject>();

                foreach (var timeGroup in afterObjects.GroupBy(h => h.StartTime))
                {
                    foreach (var note in timeGroup)
                    {
                        if (originalLNSet.Contains(note) && originalLN)
                        {
                            resultObjects.Add(note);
                            continue;
                        }

                        if (invertSpacing)
                        {
                            if (lineSpacing > 0)
                                resultObjects.Add(note);
                            else
                                resultObjects.AddNote(note.Samples, note.Column, note.StartTime);
                        }
                        else
                        {
                            if (lineSpacing > 0)
                                resultObjects.AddNote(note.Samples, note.Column, note.StartTime);
                            else
                                resultObjects.Add(note);
                        }
                    }

                    lineSpacing--;

                    if (lineSpacing < 0) lineSpacing = maxSpacing;
                }
            }

            maniaBeatmap.HitObjects = resultObjects.OrderBy(h => h.StartTime).ToList();
            maniaBeatmap.Breaks.Clear();
        }

        public static double RandDistribution(Random rng, double u, double d)
        {
            if (d <= 0) return u;

            double u1 = rng.NextDouble();
            double u2 = rng.NextDouble();
            double z = Math.Sqrt(-2 * Math.Log(u1)) * Math.Sin(2 * Math.PI * u2);
            double x = u + d * z;
            return x;
        }

        public static double TimeRound(double timedivide, double num)
        {
            double remainder = num % timedivide;
            if (remainder < timedivide / 2)
                return num - remainder;

            return num + timedivide - remainder;
        }

        /// <summary>
        /// Try to make conversion timing point(EndTime) more precision.
        /// </summary>
        /// <param name="time"></param>
        /// <param name="bpm"></param>
        /// <param name="offset"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public static double PreciseTime(double time, double bpm, double offset, double error)
        {
            foreach (int t in DIVIDE_NUMBER)
            {
                double tem = time;
                time = offset + Math.Round((time - offset) / (bpm / t)) * bpm / t;
                if (Math.Abs(time - tem) < error)
                    return time;
                else
                    time = tem;
            }

            return time;
            //try
            //{
            //}
            //catch (Exception e)
            //{
            //    Logger.Log(e.Message, level: LogLevel.Error);
            //    return null;
            //}
        }

        /// <summary>
        /// Return false if error.
        /// </summary>
        /// <returns></returns>
        public static bool SelectRandomNumberForThis(this List<int> list, Random rng, int minValue, int maxValue, int times, bool duplicate = false)
        {
            if (duplicate)
            {
                for (int i = 0; i < times; i++)
                    list.Add(rng.Next(minValue, maxValue));
            }
            else
            {
                if (maxValue - minValue < times) return false;

                while (times > 0)
                {
                    int num = rng.Next(minValue, maxValue);

                    if (!list.Contains(num))
                    {
                        list.Add(num);
                        times--;
                    }
                }
            }

            return true;
        }

        public static bool SelectRandomNumberForThis(this List<int> list, Random rng, int maxValue, int times, bool duplicate = false)
        {
            return list.SelectRandomNumberForThis(rng, 0, maxValue, times, duplicate);
        }

        public static IEnumerable<T> SelectRandom<T>(this IEnumerable<T> enumerable, Random rng, int times = 1, bool duplicate = false)
        {
            if (times <= 0) return Enumerable.Empty<T>();

            var result = new List<T>();
            var list = enumerable.ToList();

            if (duplicate)
            {
                while (times > 0)
                {
                    int index = rng.Next(list.Count);
                    result.Add(list[index]);
                    times--;
                }
            }
            else
            {
                while (times > 0)
                {
                    int index = rng.Next(list.Count);
                    result.Add(list[index]);
                    list.RemoveAt(index);
                    times--;
                }
            }

            return result.AsEnumerable();
        }

        public static T SelectRandomOne<T>(this List<T> list, Random rng)
        {
            return list[rng.Next(list.Count)];
        }

        public static void AddNote(this List<ManiaHitObject> obj, IList<HitSampleInfo> samples, int column, double startTime, double? endTime = null)
        {
            if (endTime is null || endTime == startTime)
            {
                obj.Add(new Note
                {
                    Column = column,
                    StartTime = startTime,
                    Samples = samples
                });
            }
            else
                obj.AddLNByDuration(samples, column, startTime, (double)endTime - startTime);
        }

        public static void RemoveNote(this List<ManiaHitObject> obj, int column, double startTime)
        {
            for (int i = obj.Count - 1; i >= 0; i--)
            {
                if (obj[i].Column == column && obj[i].StartTime == startTime)
                {
                    obj.Remove(obj[i]);
                    return;
                }
            }
        }

        public static void AddLNByDuration(this List<ManiaHitObject> obj, IList<HitSampleInfo> samples, int column, double startTime, double duration)
        {
            obj.Add(new HoldNote
            {
                Column = column,
                StartTime = startTime,
                Duration = duration,
                NodeSamples = [samples, Array.Empty<HitSampleInfo>()]
            });
        }

        public static bool FindOverlapInList(List<ManiaHitObject> hitobj, int column, double starttime, double endtime)
        {
            foreach (var obj in hitobj)
            {
                if (obj.Column == column && starttime <= obj.StartTime && starttime >= obj.StartTime) return true;

                if (obj.StartTime != obj.GetEndTime())
                {
                    if (obj.Column == column && starttime >= obj.StartTime && starttime <= obj.GetEndTime())
                    {
                        if (endtime != starttime)
                        {
                            if (endtime >= obj.StartTime && endtime <= obj.GetEndTime())
                                return true;
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        public static bool FindOverlapInList(ManiaHitObject hitobj, List<ManiaHitObject> objs)
        {
            return FindOverlapInList(objs, hitobj.Column, hitobj.StartTime, hitobj.GetEndTime());
        }

        public static bool FindOverlapByNote(ManiaHitObject hitobj, int column, double starttime, double endtime)
        {
            List<ManiaHitObject> onenote = [hitobj];
            return FindOverlapInList(onenote, column, starttime, endtime);
        }

        public static bool FindOverlapByList(List<ManiaHitObject> hitobj)
        {
            for (int i = 0; i < hitobj.Count; i++)
            {
                for (int j = i + 1; j < hitobj.Count; j++)
                {
                    if (hitobj[i].Column == hitobj[j].Column && hitobj[i].StartTime == hitobj[j].StartTime) return true;

                    if (hitobj[j].StartTime != hitobj[j].GetEndTime())
                    {
                        if (hitobj[i].Column == hitobj[j].Column && hitobj[i].StartTime >= hitobj[j].StartTime - 2 && hitobj[i].StartTime <= hitobj[j].GetEndTime() + 2)
                        {
                            if (hitobj[i].GetEndTime() != hitobj[j].StartTime)
                            {
                                if (hitobj[i].GetEndTime() >= hitobj[j].StartTime - 2 && hitobj[i].GetEndTime() <= hitobj[j].GetEndTime() + 2)
                                    return true;
                            }

                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static IEnumerable<T> ShuffleIndex<T>(this IEnumerable<T> list, Random rng)
        {
            var result = list.ToList();

            for (int i = 0; i < result.Count; i++)
            {
                int toIndex = rng.Next(result.Count);
                (result[i], result[toIndex]) = (result[toIndex], result[i]);
            }

            return result.AsEnumerable();
        }
    }
}
