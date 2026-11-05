using System.Text;
using SonTob.Core;
using UnityEditor;
using UnityEngine;

namespace SonTob.Editor
{
    /// <summary>지도를 고친 뒤 L(임무를 모두 도는 가장 짧은 걸음 길)을 다시 재는 메뉴.</summary>
    public static class MapTools
    {
        [MenuItem("손톱 쥐/걸음 길 L 재기")]
        public static void ReportTours()
        {
            var map = Village.CreateMap();
            var start = map.Mark(Village.RatHole);
            var sb = new StringBuilder("쥐구멍에서 임무 셋까지 걸음 길 L\n");
            foreach (var p in Village.People)
            {
                var stops = new System.Collections.Generic.List<Pos>();
                foreach (var c in p.Missions) stops.Add(map.Mark(c));
                sb.AppendLine(p.Name + ": 걸어서 " + map.ShortestTour(start, stops, false) + "칸, 개구멍 포함 " + map.ShortestTour(start, stops, true) + "칸");
            }
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("걸음 길 L", sb.ToString(), "확인");
        }
    }
}
