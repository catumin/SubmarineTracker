using System.Reflection;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.Toast;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using SubmarineTracker.Attributes;
using FFXIVClientStructs.FFXIV.Client.Game.Housing;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using SubmarineTracker.Data;
using SubmarineTracker.IPC;
using SubmarineTracker.Manager;
using SubmarineTracker.Windows;
using SubmarineTracker.Windows.Loot;
using SubmarineTracker.Windows.Helpy;
using SubmarineTracker.Windows.Config;
using SubmarineTracker.Windows.Builder;
using SubmarineTracker.Windows.Overlays;

namespace SubmarineTracker
{
    public class Plugin : IDalamudPlugin
    {
        [PluginService] public static DataManager Data { get; private set; } = null!;
        [PluginService] public static Framework Framework { get; private set; } = null!;
        [PluginService] public static CommandManager Commands { get; private set; } = null!;
        [PluginService] public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] public static ClientState ClientState { get; private set; } = null!;
        [PluginService] public static ChatGui ChatGui { get; private set; } = null!;
        [PluginService] public static ToastGui ToastGui { get; private set; } = null!;
        [PluginService] public static GameGui GameGui { get; private set; } = null!;
        [PluginService] public static SigScanner SigScanner { get; private set; } = null!;

        public static FileDialogManager FileDialogManager { get; private set; } = null!;

        public string Name => "Submarine Tracker";

        public Configuration Configuration { get; init; }
        public WindowSystem WindowSystem = new("Submarine Tracker");

        public ConfigWindow ConfigWindow { get; init; }
        public MainWindow MainWindow { get; init; }
        public BuilderWindow BuilderWindow { get; init; }
        public LootWindow LootWindow { get; init; }
        public HelpyWindow HelpyWindow { get; init; }
        public ReturnOverlay ReturnOverlay { get; init; }
        public RouteOverlay RouteOverlay { get; init; }
        public NextOverlay NextOverlay { get; init; }
        public UnlockOverlay UnlockOverlay { get; init; }

        public ConfigurationBase ConfigurationBase;

        public const string Authors = "Infi";
        public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

        private const string GithubIssue = "https://github.com/Infiziert90/SubmarineTracker/issues";
        private const string DiscordThread = "https://canary.discord.com/channels/581875019861328007/1094255662860599428";
        private const string KoFiLink = "https://ko-fi.com/infiii";

        private readonly PluginCommandManager<Plugin> CommandManager;

        private static ExcelSheet<TerritoryType> TerritoryTypes = null!;

        public readonly Notify Notify;
        public static HookManager HookManager = null!;
        public static AllaganToolsConsumer AllaganToolsConsumer = null!;
        private readonly Localization Localization = new();

        public Dictionary<uint, Submarines.Submarine> SubmarinePreVoyage = new();

        public Plugin()
        {
            ConfigurationBase = new ConfigurationBase(this);

            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);

            FileDialogManager = new FileDialogManager();

            Notify = new Notify(this);

            Loot.Initialize(this);
            Build.Initialize();
            Voyage.Initialize(this);
            Submarines.Initialize();
            TexturesCache.Initialize();
            ImportantItemsMethods.Initialize();

            Webhook.Init(Configuration);
            Helper.Initialize(this);

            HookManager = new HookManager(this);
            AllaganToolsConsumer = new AllaganToolsConsumer();

            ConfigWindow = new ConfigWindow(this);
            MainWindow = new MainWindow(this, Configuration);
            BuilderWindow = new BuilderWindow(this, Configuration);
            LootWindow = new LootWindow(this, Configuration);
            HelpyWindow = new HelpyWindow(this, Configuration);

            ReturnOverlay = new ReturnOverlay(this, Configuration);
            RouteOverlay = new RouteOverlay(this, Configuration);
            NextOverlay = new NextOverlay(this, Configuration);
            UnlockOverlay = new UnlockOverlay(this, Configuration);

            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(MainWindow);
            WindowSystem.AddWindow(BuilderWindow);
            WindowSystem.AddWindow(LootWindow);
            WindowSystem.AddWindow(HelpyWindow);

            WindowSystem.AddWindow(ReturnOverlay);
            WindowSystem.AddWindow(RouteOverlay);
            WindowSystem.AddWindow(NextOverlay);
            WindowSystem.AddWindow(UnlockOverlay);

            CommandManager = new PluginCommandManager<Plugin>(this, Commands);
            Localization.SetupWithLangCode(PluginInterface.UiLanguage);

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
            PluginInterface.LanguageChanged += Localization.SetupWithLangCode;

            TerritoryTypes = Data.GetExcelSheet<TerritoryType>()!;

            ConfigurationBase.Load();
            LoadFCOrder();

            Framework.Update += FrameworkUpdate;
            Framework.Update += Notify.NotifyLoop;

            var subDone = Submarines.KnownSubmarines.Values.Any(fc => fc.AnySubDone());
            if (Configuration.OverlayOpen || (Configuration.OverlayStartUp && subDone))
            {
                ReturnOverlay.IsOpen = true;
                // TODO Check for a valid way to uncollapse something once
                // if (Configuration is { OverlayStartUp: true, OverlayUnminimized: true } && subDone)
                // {
                //     OverlayWindow.CollapsedCondition = ImGuiCond.Appearing;
                //     OverlayWindow.Collapsed = false;
                // }
            }
        }

        public void Dispose() => Dispose(true);

        public void Dispose(bool full)
        {
            ConfigurationBase.Dispose();
            WindowSystem.RemoveAllWindows();

            ConfigWindow.Dispose();
            MainWindow.Dispose();
            BuilderWindow.Dispose();
            LootWindow.Dispose();

            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
            PluginInterface.LanguageChanged -= Localization.SetupWithLangCode;

            CommandManager.Dispose();

            HookManager.Dispose();
            TexturesCache.Instance?.Dispose();

            if (full)
            {
                Framework.Update -= FrameworkUpdate;
                Framework.Update -= Notify.NotifyLoop;
            }
        }

        [Command("/stracker")]
        [HelpMessage("Opens the tracker")]
        private void OnCommand(string command, string args)
        {
            MainWindow.IsOpen ^= true;
        }

        [Command("/sbuilder")]
        [HelpMessage("Opens the builder")]
        private void OnBuilderCommand(string command, string args)
        {
            BuilderWindow.IsOpen ^= true;
        }

        [Command("/sloot")]
        [HelpMessage("Opens the custom loot overview")]
        private void OnLootCommand(string command, string args)
        {
            LootWindow.IsOpen ^= true;
        }

        [Command("/shelpy")]
        [HelpMessage("Opens the helper window with lots of helpful information")]
        private void OnUnlockedCommand(string command, string args)
        {
            HelpyWindow.IsOpen ^= true;
        }

        [Command("/sconf")]
        [HelpMessage("Opens the config")]
        private void OnConfigCommand(string command, string args)
        {
            ConfigWindow.IsOpen ^= true;
        }

        [Command("/soverlay")]
        [HelpMessage("Opens the overlay")]
        private void OnOverlayCommand(string command, string args)
        {
            ReturnOverlay.IsOpen ^= true;

            Configuration.OverlayOpen ^= true;
            Configuration.Save();
        }

        public unsafe void FrameworkUpdate(Framework _)
        {
            // Check if we have an upload trigger
            Upload();

            var instance = HousingManager.Instance();
            if (instance == null || instance->WorkshopTerritory == null)
            {
                // Clear the cache after we left workshop
                SubmarinePreVoyage.Clear();
                return;
            }

            var local = ClientState.LocalPlayer;
            if (local == null)
                return;

            // 6.4 triggers HousingManager + WorkshopTerritory in Island Sanctuary
            if (TerritoryTypes.GetRow(ClientState.TerritoryType)!.TerritoryIntendedUse == 49)
                return;

            // Notify the user once about upload opt out
            if (Configuration.UploadNotification)
            {
                // User received the notice, so we schedule the first upload 1h after
                Configuration.UploadNotification = false;
                Configuration.UploadNotificationReceived = DateTime.Now.AddHours(1);
                Configuration.Save();

                ChatGui.Print(Utils.SuccessMessage("Important"));
                ChatGui.Print(Utils.SuccessMessage("This plugin will collect anonymized, submarine specific data. " +
                                                   "For more information on the exact data collected please see the upload tab in the plugin configuration menu.  " +
                                                   "You can opt out of any and all forms of data collection."));
            }

            var workshopData = instance->WorkshopTerritory->Submersible;
            var submarineData = workshopData.DataListSpan.ToArray();

            BuilderWindow.VoyageInterfaceSelection = 0;
            if (Configuration.AutoSelectCurrent)
            {
                var current = workshopData.DataPointerListSpan[4];
                if (current.Value != null)
                {
                    BuilderWindow.VoyageInterfaceSelection = current.Value->RegisterTime;
                    if (BuilderWindow.CurrentBuild.Rank != current.Value->RankId)
                        BuilderWindow.CacheValid = false;
                }
            }

            var possibleNewSubs = new List<Submarines.Submarine>();
            foreach (var (sub, idx) in submarineData.Where(data => data.RankId != 0).WithIndex())
            {
                possibleNewSubs.Add(new Submarines.Submarine(sub, idx));

                // We prefill the current submarines once to have the original stats
                if (!SubmarinePreVoyage.ContainsKey(sub.RegisterTime))
                    SubmarinePreVoyage[sub.RegisterTime] = new Submarines.Submarine(sub);
            }

            if (!possibleNewSubs.Any())
                return;

            Submarines.KnownSubmarines.TryAdd(ClientState.LocalContentId, Submarines.FcSubmarines.Empty);

            var fc = Submarines.KnownSubmarines[ClientState.LocalContentId];
            if (Submarines.SubmarinesEqual(fc.Submarines, possibleNewSubs) && fc.CharacterName != "")
                return;

            fc.CharacterName = Utils.ToStr(local.Name);
            fc.Tag = Utils.ToStr(local.CompanyTag);
            fc.World = Utils.ToStr(local.HomeWorld.GameData!.Name);
            fc.Submarines = possibleNewSubs;
            fc.GetUnlockedAndExploredSectors();

            foreach (var sub in submarineData.Where(data => data.RankId != 0 && data.ReturnTime != 0))
                Notify.TriggerDispatch(sub.RegisterTime, sub.ReturnTime);

            fc.Refresh = true;
            LoadFCOrder();
            ConfigurationBase.SaveCharacterConfig();
        }

        public void Sync()
        {
            foreach (var fc in Submarines.KnownSubmarines.Values)
                fc.Refresh = true;

            Storage.Refresh = true;
            ConfigurationBase.Load();
            LoadFCOrder();
        }

        public static void IssuePage() => Dalamud.Utility.Util.OpenLink(GithubIssue);
        public static void DiscordSupport() => Dalamud.Utility.Util.OpenLink(DiscordThread);
        public static void Kofi() => Dalamud.Utility.Util.OpenLink(KoFiLink);

        #region Draws
        private void DrawUI() => WindowSystem.Draw();

        public void OpenTracker() => MainWindow.IsOpen = true;
        public void OpenBuilder() => BuilderWindow.IsOpen = true;
        public void OpenLoot() => LootWindow.IsOpen = true;
        public void OpenHelpy() => HelpyWindow.IsOpen = true;
        public void OpenOverlay() => ReturnOverlay.IsOpen = true;
        public void OpenConfig() => ConfigWindow.IsOpen = true;
        #endregion

        public void LoadFCOrder()
        {
            var changed = false;
            foreach (var id in Submarines.KnownSubmarines.Keys)
                if (!Configuration.FCOrder.Contains(id))
                {
                    changed = true;
                    Configuration.FCOrder.Add(id);
                }

            if (changed)
                Configuration.Save();
        }

        public void EnsureFCOrderSafety()
        {
            var notSafe = false;
            foreach (var id in Configuration.FCOrder.ToArray())
            {
                if (!Submarines.KnownSubmarines.ContainsKey(id))
                {
                    notSafe = true;
                    Configuration.FCOrder.Remove(id);
                }
            }

            if (notSafe)
                Configuration.Save();
        }

        public void Upload()
        {
            // Check that we have permissions and a upload trigger is set
            if (Configuration is { UploadPermission: true, TriggerUpload: true })
            {
                // Check that the user had enough time to opt out after notification
                if (Configuration.UploadNotificationReceived > DateTime.Now)
                    return;

                Configuration.TriggerUpload = false;
                Configuration.UploadCounter += 1;
                Configuration.Save();

                Task.Run(() =>
                {
                    var fcLootList = Submarines.KnownSubmarines
                                               .Select(kv => kv.Value.SubLoot)
                                               .SelectMany(kv => kv.Values)
                                               .SelectMany(subLoot => subLoot.Loot)
                                               .SelectMany(innerLoot => innerLoot.Value)
                                               .Where(detailedLoot => detailedLoot is { Valid: true, Rank: > 0 })
                                               .ToList();

                    Export.UploadFullExport(fcLootList);
                });
            }
        }

        public void EntryUpload(Loot.DetailedLoot loot)
        {
            if (Configuration.UploadPermission)
            {
                // Check that the user had enough time to opt out after notification
                if (Configuration.UploadNotificationReceived > DateTime.Now)
                    return;

                Configuration.UploadCounter += 1;
                Configuration.Save();

                Task.Run(() => Export.UploadEntry(loot));
            }
        }
    }
}
