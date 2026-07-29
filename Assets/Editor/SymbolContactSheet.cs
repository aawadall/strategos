// SymbolContactSheet.cs
// Bakes a grid of symbol permutations to a PNG so rendering changes can be
// reviewed in one image instead of clicking every combination in the demo panel.
//
// Menu:  Strategos → Bake Symbol Contact Sheet
// Batch: -executeMethod Strategos.Editor.SymbolContactSheet.Bake

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Strategos.NatoSymbols;

namespace Strategos.Editor
{
    public static class SymbolContactSheet
    {
        private const int Cell = 192;
        private const int Cols = 6;
        private const string OutputPath = "Artifacts/symbol-contact-sheet.png";

        [MenuItem("Strategos/Bake Symbol Contact Sheet")]
        public static void Bake()
        {
            var cases = BuildCases();
            int rows = Mathf.CeilToInt(cases.Count / (float)Cols);
            int w = Cols * Cell;
            int h = rows * Cell;

            var sheet = new Color32[w * h];
            var bg = new Color32(244, 242, 235, 255);
            for (int i = 0; i < sheet.Length; i++) sheet[i] = bg;

            for (int i = 0; i < cases.Count; i++)
            {
                var symbol = NatoSymbolComposer.Compose(cases[i].Code, database: null);
                var sprite = NatoSymbolBaker.Bake(symbol, Cell);
                if (sprite == null || sprite.texture == null) continue;

                var px = sprite.texture.GetPixels32();
                int col = i % Cols;
                int row = i / Cols;
                // Sheet origin is bottom-left; fill rows top-down.
                int ox = col * Cell;
                int oy = (rows - 1 - row) * Cell;

                for (int y = 0; y < Cell; y++)
                for (int x = 0; x < Cell; x++)
                {
                    Color32 c = px[y * Cell + x];
                    if (c.a == 0) continue;
                    sheet[(oy + y) * w + (ox + x)] = c;
                }

                Object.DestroyImmediate(sprite.texture);
                Object.DestroyImmediate(sprite);
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(sheet);
            tex.Apply(false, false);

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllBytes(OutputPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            Debug.Log($"[SymbolContactSheet] {cases.Count} symbols -> {OutputPath}");
            for (int i = 0; i < cases.Count; i++)
                Debug.Log($"[SymbolContactSheet] cell {i} (r{i / Cols} c{i % Cols}) = {cases[i].Label}");
        }

        private readonly struct Case
        {
            public readonly string Label;
            public readonly SIDCCode Code;
            public Case(string label, SIDCCode code) { Label = label; Code = code; }
        }

        private static List<Case> BuildCases()
        {
            var list = new List<Case>();

            // Variants 11-19 against a friendly infantry company.
            string[] variantNames =
            {
                "standard", "mechanized", "motorized", "air assault", "amphibious",
                "mountain", "arctic", "heavy", "light",
            };
            for (int v = 0; v < variantNames.Length; v++)
                list.Add(new Case($"variant {variantNames[v]}", Make(entityType: 11 + v)));

            // Operational condition.
            foreach (var s in new[]
            {
                UnitStatus.Present, UnitStatus.PresentFullyCapable, UnitStatus.PresentDamaged,
                UnitStatus.PresentDestroyed, UnitStatus.PresentFullToCapacity,
                UnitStatus.AnticipatedPlanned,
            })
                list.Add(new Case($"status {s}", Make(status: s)));

            // Combat power.
            foreach (var pct in new[] { "100", "75", "40", "0" })
                list.Add(new Case($"strength {pct}%", Make(strength: pct)));

            // Field F markers.
            foreach (var m in new[]
            {
                StrengthModifier.Reinforced, StrengthModifier.Reduced,
                StrengthModifier.ReinforcedReduced,
            })
                list.Add(new Case($"field F {m}", Make(strengthMod: m)));

            // Frame shapes — the frame box was narrowed to open the text column,
            // so every identity group needs re-checking.
            foreach (var a in new[]
            {
                Affiliation.Friend, Affiliation.Hostile, Affiliation.Neutral,
                Affiliation.Unknown, Affiliation.Suspect,
            })
                list.Add(new Case($"frame {a}", Make(affiliation: a)));

            // Sector 1 (upper) modifiers, and sector 1 + sector 2 together.
            var sectorMods = new (string Name, int Code)[]
            {
                ("airborne",    SectorModifierDecorator.ModAirborne),
                ("air assault", SectorModifierDecorator.ModAirAssault),
                ("wheeled",     SectorModifierDecorator.ModWheeled),
                ("mountain",    SectorModifierDecorator.ModMountain),
                ("amphibious",  SectorModifierDecorator.ModAmphibious),
            };
            foreach (var m in sectorMods)
                list.Add(new Case($"sector1 {m.Name}", Make(modifier1: m.Code)));

            list.Add(new Case("sector1 + sector2",
                Make(modifier1: SectorModifierDecorator.ModAirborne,
                     modifier2: SectorModifierDecorator.ModWheeled)));

            // Sector 1 against a main-sector icon rather than a full-frame one.
            list.Add(new Case("sector1 + artillery",
                Make(modifier1: SectorModifierDecorator.ModAirborne,
                     entity: LandEntityCode.Artillery)));

            // Interaction cases called out in the plan.
            list.Add(new Case("variant vs sector 2",
                Make(entityType: 13, modifier2: SectorModifierDecorator.ModAirborne)));
            list.Add(new Case("HQ + damaged",
                Make(status: UnitStatus.PresentDamaged, hq: HeadquartersTaskForceDummy.Headquarters)));
            list.Add(new Case("long designation", Make(designation: "TF 2-69 AR", higher: "1/3 ID")));

            return list;
        }

        private static SIDCCode Make(
            int entityType = 11,
            UnitStatus status = UnitStatus.Present,
            HeadquartersTaskForceDummy hq = HeadquartersTaskForceDummy.None,
            int modifier1 = 0,
            int modifier2 = 0,
            LandEntityCode entity = LandEntityCode.Infantry,
            string strength = "100",
            StrengthModifier strengthMod = StrengthModifier.None,
            string designation = "1-7 IN",
            string higher = "3 ID",
            Affiliation affiliation = Affiliation.Friend)
        {
            string raw = string.Format(
                "10{0}{1}{2:D2}{3}{4}{5:D2}{6:D2}{7:D2}{8:D2}{9:D2}{10:D2}",
                (int)SymbolContext.Reality,
                (int)affiliation,
                (int)SymbolSet.LandUnit,
                (int)status,
                (int)hq,
                (int)Echelon.Company,
                (int)entity,
                entityType,
                0,
                modifier1,
                modifier2);

            if (!SIDCParser.TryParse(raw, out var code))
                code = new SIDCCode { Raw = raw };

            code.Designation = designation;
            code.HigherFormation = higher;
            code.StrengthLabel = strength;
            code.StrengthModifier = strengthMod;
            return code;
        }
    }
}
#endif
