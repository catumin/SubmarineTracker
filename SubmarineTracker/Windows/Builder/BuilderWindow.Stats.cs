using SubmarineTracker.Data;
using static SubmarineTracker.Utils;

namespace SubmarineTracker.Windows.Builder;

public partial class BuilderWindow
{
    private void BuildStats(ref Submarines.Submarine sub)
    {
        if (ImGui.BeginChild("SubStats", new Vector2(0, 0)))
        {
            var build = CurrentBuild.GetSubmarineBuild;

            // Reset to custom build if not equal anymore
            if (sub.IsValid() && !build.EqualsSubmarine(sub))
                CurrentBuild.OriginalSub = 0;

            var startPoint = ExplorationSheet.First(r => r.Map.Row == CurrentBuild.Map + 1);

            var optimizedPoints = CurrentBuild.OptimizedRoute.Prepend(startPoint).ToList();
            var optimizedDuration = Voyage.CalculateDuration(optimizedPoints, build);
            var breakpoints = Sectors.CalculateBreakpoint(CurrentBuild.Sectors);
            var expPerMinute = 0.0;
            var totalExp = 0u;
            var repairAfter = 0;
            if (optimizedDuration != 0 && CurrentBuild.OptimizedDistance != 0)
            {
                totalExp = Sectors.CalculateExpForSectors(CurrentBuild.OptimizedRoute, CurrentBuild.GetSubmarineBuild);
                expPerMinute = totalExp / (optimizedDuration / 60.0);
                repairAfter = CurrentBuild.CalculateUntilRepair();
            }

            var tanks = 0u;
            if (Plugin.AllaganToolsConsumer.IsAvailable)
            {
                // build cache if needed
                Storage.BuildStorageCache();
                if (Storage.StorageCache.TryGetValue(Plugin.ClientState.LocalContentId, out var cachedItems) &&
                    cachedItems.TryGetValue((uint)Items.Tanks, out var temp))
                    tanks = temp.Count;
            }

            if (ImGui.BeginTable("##buildColumn", 2, ImGuiTableFlags.SizingFixedFit))
            {
                ImGui.TableSetupColumn("##title");
                ImGui.TableSetupColumn("##content");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted("Selected Build:");
                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.DalamudOrange, $"{CurrentBuild} (Rank {CurrentBuild.Rank})");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted("Optimized Route:");
                ImGui.TableNextColumn();
                SelectedRoute();

                ImGui.EndTable();
            }

            ImGui.TextUnformatted("Calculated Stats:");

            if (ImGui.BeginTable("##statsColumn", 6))
            {
                ImGui.TableSetupColumn("##stat1", 0, 0.55f);
                ImGui.TableSetupColumn("##count1", 0, 0.72f);
                ImGui.TableSetupColumn("##stat2", 0, 0.44f);
                ImGui.TableSetupColumn("##count2", 0, 0.5f);
                ImGui.TableSetupColumn("##stat3", 0, 0.4f);
                ImGui.TableSetupColumn("##count3", 0, 0.5f);

                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.HealerGreen, $"Surveillance");
                ImGui.TableNextColumn();
                SelectRequiredColor(breakpoints.T2, build.Surveillance, breakpoints.T3);

                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.HealerGreen, $"Retrieval");
                ImGui.TableNextColumn();
                SelectRequiredColor(breakpoints.Normal, build.Retrieval, breakpoints.Optimal);

                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.HealerGreen, $"Favor");
                ImGui.TableNextColumn();
                SelectRequiredColor(breakpoints.Favor, build.Favor);

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.HealerGreen, $"Speed");
                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.HealerGreen, $"{build.Speed}");

                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.HealerGreen, $"Range");
                ImGui.TableNextColumn();
                SelectRequiredColor((int) CurrentBuild.OptimizedDistance, build.Range);

                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.HealerGreen, $"Fuel");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{CurrentBuild.FuelCost}{(tanks > 0 ? $" / {tanks}" : "")}");

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.HealerGreen, $"Duration");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{ToTime(TimeSpan.FromSeconds(optimizedDuration))}");

                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.HealerGreen, $"Exp");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{totalExp:N0}");

                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.HealerGreen, $"Exp/Min");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{expPerMinute:F}");

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.HealerGreen, $"Repair");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{build.RepairCosts} after {repairAfter} voyages");

                ImGui.EndTable();
            }
        }
        ImGui.EndChild();
    }

    public static void SelectRequiredColor(int minRequired, int current, int maxRequired = -1)
    {
        if (minRequired == 0)
        {
            ImGui.TextUnformatted($"{current}");
        }
        else if (minRequired > current)
        {
            ImGui.TextColored(ImGuiColors.DalamudRed, $"{current} ({minRequired})");
        }
        else if (maxRequired == -1)
        {
            if (minRequired == current)
                ImGui.TextColored(ImGuiColors.HealerGreen, $"{current}");
            else
                ImGui.TextColored(ImGuiColors.ParsedGold, $"{current} ({minRequired})");
        }
        else
        {
            if (maxRequired == current)
                ImGui.TextColored(ImGuiColors.HealerGreen, $"{current}");
            else if (current >= minRequired && current < maxRequired)
                ImGui.TextColored(ImGuiColors.ParsedPink, $"{current} ({maxRequired})");
            else
                ImGui.TextColored(ImGuiColors.ParsedGold, $"{current} ({maxRequired})");
        }
    }

    public void SelectedRoute()
    {
        if (CurrentBuild.OptimizedRoute.Any())
        {
            var startPoint = ExplorationSheet.First(r => r.Map.Row == CurrentBuild.Map + 1);
            ImGui.TextColored(ImGuiColors.DalamudOrange, string.Join(" -> ", CurrentBuild.OptimizedRoute.Where(p => p.RowId > startPoint.RowId).Select(p => NumToLetter(p.RowId - startPoint.RowId))));
        }
        else
        {
            ImGui.TextColored(ImGuiColors.DalamudOrange, "No Selection");
        }
    }
}
