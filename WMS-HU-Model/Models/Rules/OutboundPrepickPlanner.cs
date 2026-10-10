using System;
using System.Collections.Generic;
using System.Linq;
using WMS_HU_Model.Models.Outbound.Formal;

namespace WMS_HU_Model.Rules
{
    // Preview only. Never convert these suggestions to formal reservations.
    public class OutboundPrepickPlanner
    {
        private const int CandidateLimit = 100;
        private const int SearchLimit = 200000;
        // A missing/ambiguous actual revision disables SET preference for the whole group.
        public static void SetPackConversion(OutboundPrepickGroup group, IEnumerable<OutboundOrderLineDto> lines,
            IEnumerable<PrepickPackMaster> masters)
        {
            group.PcsPerSetQty = null;
            var snapshots = lines.Select(x => x.PcsPerSetQtySnapshot).Distinct().ToList();
            if (snapshots.Count != 1 || !snapshots[0].HasValue || snapshots[0].Value <= 0) return;
            decimal size = snapshots[0].Value;
            var packs = group.Boxes.Where(x => x.Eligible && !x.OwnReservation).SelectMany(x => x.InnerPacks).ToList();
            if (packs.Count == 0) return;
            foreach (var revision in packs.Select(x => x.MaterialRevision).Distinct()) {
                if (string.IsNullOrWhiteSpace(revision)) return;
                var matches = masters.Where(x => string.Equals(x.MaterialCode, group.MaterialCode, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.MaterialRevision, revision, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count != 1 || matches[0].PcsPerSetQty != size) return;
            }
            group.PcsPerSetQty = size;
        }
        public void Build(OutboundPrepickGroup group)
        {
            foreach (var b in group.Boxes) {
                b.SuggestedQty=0; b.Recommendation=null;
                foreach (var p in b.InnerPacks) p.SuggestedQty=0;
            }
            var duplicatePacks=new HashSet<long>(group.Boxes.SelectMany(x=>x.InnerPacks)
                .GroupBy(x=>x.InnerHuId).Where(x=>x.Count()>1).Select(x=>x.Key));
            foreach(var b in group.Boxes.Where(x=>x.Eligible)) {
                if(b.Qty<=0 || b.InnerPacks.Count==0 || b.InnerPacks.Any(x=>x.Qty<=0 || duplicatePacks.Contains(x.InnerHuId))
                    || b.InnerPacks.Sum(x=>x.Qty)!=b.Qty) {
                    b.Eligible=false;
                    if(!b.Reasons.Contains("BAD_PACKAGING")) b.Reasons.Add("BAD_PACKAGING");
                }
            }
            var boxes=group.Boxes.Where(x=>x.Eligible && !x.OwnReservation)
                .OrderBy(x=>x.WeekSortKey).ThenBy(x=>x.BoxHuId).ToList();
            group.EligibleQty=boxes.Sum(x=>x.Qty);
            group.ShortageQty=Math.Max(0,group.RequiredQty-group.EligibleQty);
            if (group.RequiredQty<=0) { group.PlanCode="NO_DEMAND"; return; }
            if (boxes.Count>CandidateLimit) { group.PlanCode="CANDIDATE_LIMIT"; return; }
            if (group.ShortageQty>0) { group.PlanCode="SHORTAGE"; return; }
            try {
                var exact=Exact(boxes.Select(x=>x.Qty).ToList(),group.RequiredQty);
                if (exact!=null) {
                    foreach (int i in exact) {
                        boxes[i].SuggestedQty=boxes[i].Qty; boxes[i].Recommendation="WHOLE_BOX";
                        foreach (var p in boxes[i].InnerPacks) p.SuggestedQty=p.Qty;
                    }
                    group.PlanCode="WHOLE_BOX"; return;
                }
                var packs=boxes.SelectMany(x=>x.InnerPacks).OrderBy(x=>x.WeekSortKey).ThenBy(x=>x.InnerHuId).ToList();
                if (packs.Count>CandidateLimit) { group.PlanCode="CANDIDATE_LIMIT"; return; }
                exact=Exact(packs.Select(x=>x.Qty).ToList(),group.RequiredQty);
                if (exact!=null) {
                    foreach (int i in exact) packs[i].SuggestedQty=packs[i].Qty;
                    Mark(boxes); group.PlanCode="SPLIT_OUTER"; return;
                }
                if (!string.Equals(group.Uom,"PCS",StringComparison.OrdinalIgnoreCase)
                    || group.RequiredQty!=decimal.Truncate(group.RequiredQty)
                    || packs.Any(x=>x.Qty!=decimal.Truncate(x.Qty))) {
                    group.PlanCode="NO_EXACT_COMBINATION"; return;
                }
                decimal remaining=group.RequiredQty;
                // Keep complete boxes first, then complete packs, then one partial source pack.
                foreach (var b in boxes.Where(x=>x.Qty>0)) {
                    if (b.Qty>remaining) continue;
                    foreach(var p in b.InnerPacks) p.SuggestedQty=p.Qty;
                    remaining-=b.Qty;
                }
                foreach (var p in packs.Where(x=>x.SuggestedQty==0)) {
                    if (p.Qty>remaining) continue;
                    p.SuggestedQty=p.Qty; remaining-=p.Qty;
                }
                if (remaining>0) {
                    var size=group.PcsPerSetQty;
                    bool preferSet=size.HasValue && size.Value>0 && size.Value==decimal.Truncate(size.Value);
                    var source=packs.Where(x=>x.SuggestedQty==0 && x.Qty>remaining)
                        .OrderBy(x=>x.WeekSortKey)
                        .ThenBy(x=>preferSet && (x.Qty-remaining)%size.Value==0 ? 0 : 1)
                        .ThenBy(x=>preferSet && x.Qty%size.Value!=0 ? 0 : 1)
                        .ThenBy(x=>x.Qty).ThenBy(x=>x.InnerHuId).First();
                    source.SuggestedQty=remaining;
                }
                Mark(boxes); group.PlanCode="SPLIT_INNER";
            } catch (SearchTooLargeException) { group.PlanCode="CANDIDATE_LIMIT"; }
        }
        private static void Mark(List<OutboundPrepickBox> boxes)
        {
            foreach(var b in boxes) {
                b.SuggestedQty=b.InnerPacks.Sum(x=>x.SuggestedQty);
                if(b.SuggestedQty==0) continue;
                b.Recommendation=b.SuggestedQty==b.Qty ? "WHOLE_BOX"
                    : b.InnerPacks.Any(x=>x.SuggestedQty>0 && x.SuggestedQty<x.Qty) ? "SPLIT_INNER" : "SPLIT_OUTER";
            }
        }
        private static List<int> Exact(List<decimal> quantities,decimal target)
        {
            var suffix=new decimal[quantities.Count+1];
            for(int i=quantities.Count-1;i>=0;i--) suffix[i]=suffix[i+1]+quantities[i];
            var dead=new HashSet<Tuple<int,decimal>>(); var chosen=new List<int>(); int visited=0;
            return Search(quantities,suffix,0,target,chosen,dead,ref visited) ? chosen : null;
        }
        private static bool Search(List<decimal> q,decimal[] suffix,int i,decimal remaining,
            List<int> chosen,HashSet<Tuple<int,decimal>> dead,ref int visited)
        {
            if(++visited>SearchLimit) throw new SearchTooLargeException();
            if(remaining==0) return true;
            if(i==q.Count || remaining<0 || suffix[i]<remaining) return false;
            var key=Tuple.Create(i,remaining); if(dead.Contains(key)) return false;
            if(q[i]>0 && q[i]<=remaining) {
                chosen.Add(i);
                if(Search(q,suffix,i+1,remaining-q[i],chosen,dead,ref visited)) return true;
                chosen.RemoveAt(chosen.Count-1);
            }
            if(Search(q,suffix,i+1,remaining,chosen,dead,ref visited)) return true;
            dead.Add(key); return false;
        }
        private class SearchTooLargeException : Exception { }
    }
}
