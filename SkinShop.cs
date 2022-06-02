// Requires: MemoryCache

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("Skin Shop", "Rustoholics", "0.5.2")]
    [Description("A GUI skin shop to allow players to buy custom skins")]
    
    public class SkinShop : CovalencePlugin
    {
        #region  Dependencies

        [PluginReference]
        private MemoryCache MemoryCache;

        [PluginReference] private Plugin ImageLibrary, Economics, ServerRewards;
        
        #endregion

        #region Variables
        private string _downloadJsonUrl = "https://www.dropbox.com/s/vyrq104kzxxomo1/database.json?dl=1";
        
        private Dictionary<string, WorkshopResult> _cache = new Dictionary<string, WorkshopResult>();

        private Dictionary<string, GuiHelper> _skinGui = new Dictionary<string, GuiHelper>();
        
        private Dictionary<string, List<WorkShopItem>> _ownedItems = new Dictionary<string, List<WorkShopItem>>();

        private Dictionary<string, WorkShopItem> _skinDatabase = new Dictionary<string, WorkShopItem>();

        private List<string> _needsWriting = new List<string>();

        private const string welcomePackPermission = "skinshop.welcomepack";
        private const string adminPermission = "skinshop.admin";
        private const string blacklistPermission = "skinshop.blacklist";
        private const string useSkinshopPermission = "skinshop.use";
        private const string vipPermissions = "skinshop.vip";

        private bool _categoryIconsLoaded = false;

        /* DO NOT EDIT THIS LIST, if you want to change the category menu display, then edit the language file instead */
        private static SortedDictionary<string, string> _categories = new SortedDictionary<string, string>
        {
            {"Bandana","mask.bandana"},
            {"Balaclava","mask.balaclava"},
            {"Beenie Hat","hat.beenie"},
            {"Burlap Shoes","burlap.shoes"},
            {"Burlap Shirt","burlap.shirt"},
            {"Burlap Pants","burlap.trousers"},
            {"Burlap Headwrap","burlap.headwrap"},
            {"Bucket Helmet","bucket.helmet"},
            {"Boonie Hat","hat.boonie"},
            {"Cap","hat.cap"},
            {"Collared Shirt","shirt.collared"},
            {"Coffee Can Helmet","coffeecan.helmet"},
            {"Deer Skull Mask","deer.skull.mask"},
            {"Hide Skirt","attire.hide.skirt"},
            {"Hide Shirt","attire.hide.vest"},
            {"Hide Pants","attire.hide.pants"},
            {"Hide Shoes","attire.hide.boots"},
            {"Hide Halterneck","attire.hide.helterneck"},
            {"Hoodie","hoodie"},
            {"Hide Poncho","attire.hide.poncho"},
            {"Leather Gloves","burlap.gloves"},
            {"Long TShirt","tshirt.long"},
            {"Metal Chest Plate","metal.plate.torso"},
            {"Metal Facemask","metal.facemask"},
            {"Miner Hat","hat.miner"},
            {"Pants","pants"},
            {"Roadsign Vest","roadsign.jacket"},
            {"Roadsign Pants","roadsign.kilt"},
            {"Riot Helmet","riot.helmet"},
            {"Snow Jacket","jacket.snow"},
            {"Shorts","pants.shorts"},
            {"Tank Top","shirt.tanktop"},
            {"TShirt","tshirt"},
            {"Vagabond Jacket","jacket"},
            {"Work Boots","shoes.boots"},
            {"AK47","rifle.ak"},
            {"Bolt Rifle","rifle.bolt"},
            {"Bone Club","bone.club"},
            {"Bone Knife","knife.bone"},
            {"Crossbow","crossbow"},
            {"Hunting Bow","bow.hunting"},
            {"Double Barrel Shotgun","shotgun.double"},
            {"Eoka Pistol","pistol.eoka"},
            {"F1 Grenade","grenade.f1"},
            {"Longsword","longsword"},
            {"Mp5","smg.mp5"},
            {"Pump Shotgun","shotgun.pump"},
            {"Rock","rock"},
            {"Salvaged Hammer","hammer.salvaged"},
            {"Salvaged Icepick","icepick.salvaged"},
            {"Satchel Charge","explosive.satchel"},
            {"Semi-Automatic Pistol","pistol.semiauto"},
            {"Stone Hatchet","stonehatchet"},
            {"Stone Pick Axe","stone.pickaxe"},
            {"Sword","salvaged.sword"},
            {"Thompson","smg.thompson"},
            {"Hammer","hammer"},
            {"Hatchet","hatchet"},
            {"Pick Axe","pickaxe"},
            {"Revolver","pistol.revolver"},
            {"Rocket Launcher","rocket.launcher"},
            {"Semi-Automatic Rifle","rifle.semiauto"},
            {"Waterpipe Shotgun","shotgun.waterpipe"},
            {"Custom SMG","smg.2"},
            {"Python","pistol.python"},
            {"LR300","rifle.lr300"},
            {"Combat Knife","knife.combat"},
            {"Armored Door","door.hinged.toptier"},
            {"Concrete Barricade","barricade.concrete"},
            {"Large Wood Box","box.wooden.large"},
            {"Reactive Target","target.reactive"},
            {"Sandbag Barricade","barricade.sandbags"},
            {"Sleeping Bag","sleepingbag"},
            {"Sheet Metal Door","door.hinged.metal"},
            {"Water Purifier","water.purifier"},
            {"Wood Storage Box","box.wooden"},
            {"Wooden Door","door.hinged.wood"},
            {"Acoustic Guitar","fun.guitar"},
            {"Rug","rug"},
            {"Bearskin Rug","rug.bear"},
            {"Sheet Metal Double Door","door.double.hinged.metal"},
            {"Wooden Double Door","door.double.hinged.wood"},
            {"Armored Double Door","door.double.hinged.toptier"},
            {"Garage Door","wall.frame.garagedoor"},
            {"L96","rifle.l96"},
            {"M249","lmg.m249"},
            {"M39","rifle.m39"},
            {"Table","table"},
            {"Chair","chair"},
            {"Locker","locker"},
            {"Furnace","furnace"},
            {"Vending Machine", "vending.machine"}
        };

        #endregion
        
        #region Config
        private Configuration _config;
        protected override void SaveConfig() => Config.WriteObject(_config);
        protected override void LoadDefaultConfig() => _config = new Configuration();

        private class Configuration
        {
            [JsonProperty(PropertyName = "LogLevel (debug | info | none)")]
            public string LogLevel = "info";

            public bool SkinsPacksEnabled = true;

            public Dictionary<int, SkinPack> SkinPacks = new Dictionary<int, SkinPack>
            {
                {
                    0, new SkinPack
                    {
                        SkinId = 2631235609,
                        NumberOfSkins = 3,
                        ImageUrl =
                            "https://steamuserimages-a.akamaihd.net/ugc/1765966378913384293/506A7A4ADA3C8C6B9729700BB21E0AB6F156E9B4/",
                        PackName = "Bronze Skin Pack",
                        Price = 1000
                    }
                },
                {
                    1, new SkinPack
                    {
                        SkinId = 2631961805,
                        NumberOfSkins = 6,
                        ImageUrl =
                            "https://steamuserimages-a.akamaihd.net/ugc/1765966378917593382/7E8B5C1F34440329A4C993B6E751609A148045B3/",
                        PackName = "Gold Skin Pack",
                        Price = 1500
                    }
                }
            };

            [JsonProperty(PropertyName =
                "Give Welcome Skin Pack index to new players ([0,0] would give 2 of the first packs, empty [] for disable)")]
            public int[] GiveWelcomePacks = new int[] {0};

            public bool EnabledCategoryIcons = true;

            public ulong[] BlackListedSkins = {0};

            public string[] HideCategories = { };

            public double DefaultSkinPrice = 1000;
			
			public int DefaultSkinsCountPerItem = 90;

            public double[] VipDiscounts = {10d, 20d};

            public double DefaultInstantPricePrice = 50;
            
            [JsonProperty(PropertyName = "Show the category selection page as the shop landing page")]
            public bool ShowCategorySelectLanding = true;
            [JsonProperty(PropertyName = "Default category to show (if category selection landing page is false)")]
            public string DefaultCategory = "AK47";

            [JsonProperty(PropertyName = "Currency Plugin (can be 'Economics' or 'ServerRewards')")]
            public string CurrencyPlugin = "Economics";
            
            public ulong[] HumanNpcIds = {0};

            [JsonProperty(PropertyName = "Convert all Wrapped Gifts to Skin Pack")]
            public bool ConvertGiftsToPacks = false;

            public bool AutomaticallyRemoveMissingImages = true;

            public string SkinPackImageUrl = "https://rustoholics.com/img/skinpackopen4.png";

            public string SearchIconUrl = "https://www.iconsdb.com/icons/preview/white/search-3-xxl.png";
            public string BlacklistIconUrl = "https://www.iconsdb.com/icons/preview/white/trash-2-xxl.png";

            public string DiscordApiKey = "";

            public string DiscordChannelId = "";
            
            public Dictionary<string, string> Color = new Dictionary<string, string>
            {
                {"Red", "0.85 0.33 0.25"},
                {"Green","0.42 0.55 0.22"},
                {"Yellow","0.82 0.53 0.13"},
                {"Blue", "0.22 0.35 0.55"},
                {"Grey","0.2 0.2 0.2"},
                {"White","1.0 1.0 1.0"}
            };

            public bool LoadSkinsFromDatabase = true;
            public bool EnableCaching = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();

                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }
        #endregion
        
        #region Language
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        
        protected override void LoadDefaultMessages()
        {
            var phrases = new Dictionary<string, string>
            {
                ["DiscordOpenPackMessage"] = ":gift: ({0:HH:mm}) **{1}** has opened a {2}",
                ["GuiCloseButton"] = "(X) Close",
                ["GuiTitleOpenSkinPack"] = "Open Skin Pack",
                ["GuiButtonViewAllMySkins"] = "View All My Skins",
                ["WithdrawCoinsFailed"] = "We could not collect enough coins to buy this item",
                ["NotEnoughCoinsToBuy"] = "You need {0} to buy this item!",
                ["NoRoomInInventory"] = "You do not have any room in your inventory to buy this pack",
                ["GuiButtonSkinEquipped"] = "√ Equipped",
                ["GuiButtonSkinUnequipped"] = "Unequipped",
                ["GuiButtonBuyNow"] = "Buy Now: {0} coins",
                ["GuiButtonBuyNowShort"] = "Buy Now: {0}",
                ["GuiTitleSkinShop"] = "Skin Shop",
                ["GuiButtonSelectCategory"] = "Select Category",
                ["GuiButtonSkinPacks"] = "Skin Packs",
                ["GuiButtonMySkins"] = "My Skins",
                ["CommandDatabaseBuilding"] = "Starting database building, but it could take a while",
                ["AccessDenied"] = "Access Denied",
                ["InstantSellDescription"] = "You can sell  this \n skin instantly for {0} coins",
                ["InvalidPlayer"] = "Invalid Player",
                ["ItemNotFound"] = "Item Not Found",
                ["ItemSold"] = "Skin has been sold for {0} coins",
                ["PluginNotInstalled"] = "The {0} plugin is not installed",
                ["SavingDataMessage"] = "Saving Skin Data: {0} players",
                ["ButtonSell"] = "Sell",
                ["ButtonSellNow"] = "Sell Now",
                ["SkinDatabaseLoaded"] = "Skin database loaded with {0} skins",
                ["FilesNotFound"] = "Files not found (probably directory not created yet",
                ["UserIDMatchNotFound"] = "No UID match found in filename",
                ["SkinsLoadedForUser"] = "Loading skins for user {0}",
                ["DownloadFailed"] = "Download of JSON database failed: {0}",
                ["DownloadEmpty"] = "Downloaded Database JSON was empty",
                ["MissingImage"] = "There was a missing image {0}",
                ["ItemDeleted"] = "Item {0} has been deleted from the skin database",
                ["IconsLoaded"] = "{0} category icons loaded",
                ["DatabaseBuildCompleted"] = "Database build has been completed",
                ["DatabaseCategoryCompleted"] = "{0} single category build has been completed!",
                ["DatabasePageDone"] = "{0} ({1}): Page {2}",
                ["DatabaseCategoryPageDone"] = "{0}: Page {1}",
                ["InstantSell"] = "Instant Sell",
                ["ButtonApply"] = "Apply",
                ["ItemNoSkin"] = "This {0} does not have a custom skin",
                ["NotLookingAtItem"] = "You are not looking at any item",
                ["SkinIdIs"] = "This skin ID for this {0} is {1}",
                ["SkinBlackListed"] = "Skin for {0} has been blacklisted",
                ["MustLookAtCustomSkin"] = "You must look at an item with a custom skin",
                ["InvalidSkinId"] = "Invalid Skin ID"
            };
            foreach (var cat in _categories)
            {
                phrases[cat.Value] = cat.Key;
            }
            lang.RegisterMessages(phrases, this);
        }
        
        #endregion
        
        #region Objects

        public class GuiOptions
        {
            public bool ShowCategories = false;
            public bool ShowSkinPacks = false;
            public string ShowSkinProfile = "";
        }
        
        public class SkinPack
        {
            public ulong SkinId;
            public string ImageUrl;
            public int NumberOfSkins = 3;
            public string PackName = "Skin Pack";
            public double Price = 1000;
            public ulong[] PossibleSkinIds = Array.Empty<ulong>();
            public string[] PossibleSkinCategories = Array.Empty<string>();
        }

        public class WorkshopResult
        {
            private List<WorkShopItem> Items = new List<WorkShopItem>();

            public int Page = 1;
            public int TotalResults;
            public int TotalPages => (int) Math.Ceiling(TotalResults / Convert.ToDouble(PerPage));
            public int PerPage = 18;
            public List<string> Categories = new List<string>();
            public string Search = "";
            public string OwnerId = "";
            
            public string Url { get { return GetUrl();} }

            public List<WorkShopItem> GetItems()
            {
                return Items;
            }

            public void RemoveOwner()
            {
                OwnerId = "";
            }

            public void AddItem(WorkShopItem item)
            {
                Items.Add(item);
            }

            public string GetUrl()
            {
                var url = "https://steamcommunity.com/workshop/browse/?appid=252490";
                foreach (var c in Categories)
                {
                    url += "&requiredtags[]="+(Uri.EscapeDataString(c));
                }

                if (!string.IsNullOrEmpty(Search))
                {
                    url += "&searchtext=" + (Uri.EscapeDataString(Search));
                }
                url += "&p="+Convert.ToString(Page)+"&numperpage=" + Convert.ToString(PerPage);
                return url;
            }

            public void LoadOwned(Dictionary<string, List<WorkShopItem>> allOwned)
            {
                if (OwnerId == "" || !allOwned.ContainsKey(OwnerId)) return;

                var query = allOwned[OwnerId].Where(i => i.Title.IndexOf(Search, 0, StringComparison.CurrentCultureIgnoreCase) >= 0 || i.Shortname.IndexOf(Search, 0, StringComparison.CurrentCultureIgnoreCase) >= 0);
                
                Items = new List<WorkShopItem>();
                foreach (var i in query.OrderBy(i => i.Shortname).Skip((Page-1) * PerPage).Take(PerPage))
                {
                    Items.Add(i);
                }
                TotalResults = allOwned[OwnerId].Count;
            }

            public void LoadOwned(Dictionary<string, List<WorkShopItem>> allOwned, string itemId)
            {
                if (OwnerId == "" || !allOwned.ContainsKey(OwnerId)) return;

                foreach (var i in allOwned[OwnerId])
                {
                    if (i.Id == itemId)
                    {
                        Items.Add(i);
                        TotalResults = 1;
                        return;
                    }
                }
            }

            public string Escape()
            {
                return Uri.EscapeDataString(JsonConvert.SerializeObject(this));
            }
        }
        
        public class WorkShopItem
        {
            public string Title;
            public string Description;
            public string Image;
            public string Id;
            public double Price;
            public string Category;
            public bool Wrapped = true;
            public bool Equipped = false;
            public string Shortname => _categories[Category];

            public double GetPrice(double defaultPrice, double discount)
            {
                var p = Price;
                if (p <= 0)
                {
                    p = defaultPrice;
                }
                
                return p * ((100-discount) / 100);
            }

            public double GetInstantSellPrice(double defaultPrice)
            {
                return defaultPrice;
            }

            public string Escape()
            {
                return Uri.EscapeDataString(JsonConvert.SerializeObject(this));
            }
                
        }
        
        

        
        #endregion
        
        #region Hooks

        private bool? CanStackItem(Item original, Item target)
        {
            if (IsSkinPack(original) || IsSkinPack(target))
            {
                return false;
            }
            return null;
        }
        
        ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            if (IsSkinPack(container.parent)) return ItemContainer.CanAcceptResult.CannotAccept;
            
            return null;
        }

        private void OnServerInitialized()
        { 
            if (ImageLibrary == null || !ImageLibrary.IsLoaded)
            {
                LogError("ImageLibrary Plugin is required, get it at https://umod.org");
            }
            if (!HasEconomicsPlugin())
            {
                LogError("Economics or ServerRewards Plugin is required, get it at https://umod.org");
            }

            for (var x = 0; x < _config.VipDiscounts.Length; x++)
            {
                permission.RegisterPermission(vipPermissions+(x+1).ToString(), this);
            }
            
            if (ImageLibrary == null || !ImageLibrary.IsLoaded) return;
            
            if (!ImageLibrary.Call<bool>("HasImage", _config.SearchIconUrl, (ulong) 0))
            {
                ImageLibrary.Call<bool>("AddImage", _config.SearchIconUrl,
                    _config.SearchIconUrl, (ulong) 0);
                
                ImageLibrary.Call<bool>("AddImage", _config.BlacklistIconUrl,
                    _config.BlacklistIconUrl, (ulong) 0);
                
            }
            
            if (!ImageLibrary.Call<bool>("HasImage", _config.SkinPackImageUrl, (ulong) 0))
            {
                ImageLibrary.Call<bool>("AddImage", _config.SkinPackImageUrl,
                    _config.SkinPackImageUrl, (ulong) 0);
            }
            
            LoadData();

            LoadDatabase();

            LoadIconImages();


        }
        
        private void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            if (task == null || task.owner == null || !task.owner.userID.IsSteamId()) return;

            SetSkin(task.owner, item);
        }
        
        private void OnItemPickup(Item item, BasePlayer player)
        {
            if (item != null && player != null)
            {
                SetupSkinPack(item);
                SetSkin(player, item);
            }
        }
        
        void OnPlayerConnected(BasePlayer player)
        {
            if (!_config.SkinsPacksEnabled || _config.GiveWelcomePacks.Length <= 0) return;
            
            if (!permission.UserHasPermission(player.UserIDString, welcomePackPermission))
            {
                permission.GrantUserPermission(player.UserIDString, welcomePackPermission, this);
                foreach (var packId in _config.GiveWelcomePacks)
                {
                    GiveSkinPack(player, _config.SkinPacks[packId]);
                }
            }
        }
        
        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            SetupSkinPack(item);
            if (container.playerOwner && item != null)
            {
                SetSkin(container.playerOwner, item);
            }
        }
        
        private void OnServerSave() => timer.Once(UnityEngine.Random.Range(0f, 60f), SaveData);
        
        
        
        void Unload()
        {
            foreach (var gui in _skinGui)
            {
                var player = BasePlayer.FindByID(Convert.ToUInt64(gui.Key));
                if (player == null || !player.IsConnected) return;
                gui.Value.Close();
            }
            _skinGui.Clear();
            
            SaveData();
            SaveSkinDatabase();
        }
        
        object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (IsSkinPack(item) && action == "open")
            {
                item.Remove();
                var items = OpenPack(player, GetSkinPack(item));  
                ShowPacksGui(player, items);
                return true;
            }
            return null;
        }

        #endregion

        #region Skins GUI

        void CloseSkinGui(BasePlayer player)
        {
            if (player == null || !_skinGui.ContainsKey(player.UserIDString)) return;

            var x = _skinGui[player.UserIDString].Close();
            Puts("Destroyed " + x + " elements");
            _skinGui.Remove(player.UserIDString);
        }

        void PrepareShowSkinGui(BasePlayer player, WorkshopResult workshop, GuiOptions options = null)
        {
            if (options == null) options = new GuiOptions();

            if (workshop.OwnerId != "")
            {
                if (options.ShowSkinProfile != "")
                {
                    workshop.LoadOwned(_ownedItems, options.ShowSkinProfile);
                }
                else
                {
                    workshop.LoadOwned(_ownedItems);
                }
                ShowSkinGui(player, workshop, options);

                return;
            }

            if (options.ShowCategories || options.ShowSkinPacks)
            {
                ShowSkinGui(player, workshop, options);
                return;
            }

            if (workshop.Categories.Count == 0 && workshop.OwnerId == "") workshop.Categories = new List<string> {_config.DefaultCategory};
            
            // Loading from database instead of from crawling steam workshop URL (much faster)
            if (_config.LoadSkinsFromDatabase)
            {
                var result = _skinDatabase.AsEnumerable();
                result = result.Where(x => workshop.Categories.Contains(x.Value.Category));
                result = result.Where(x => !_config.BlackListedSkins.Contains(Convert.ToUInt64(x.Key)));

                if (!string.IsNullOrEmpty(workshop.Search))
                {
                    result = result.Where(x =>
                        x.Value.Title.ToLower().Contains(workshop.Search.ToLower()) ||
                        x.Value.Description.ToLower().Contains(workshop.Search.ToLower()));
                }
                
                result = result.OrderByDescending(x => Convert.ToUInt64(x.Value.Id));
                
                workshop.TotalResults = result.Count();
                foreach (var r in result.Skip(Math.Max(0,workshop.Page-1) * workshop.PerPage).Take(workshop.PerPage))
                {
                    workshop.AddItem(r.Value);
                }

                ShowSkinGui(player, workshop, options);
                return;
            }

            var cacheKey = Hash(workshop.Url);
            var cache = MemoryCache.Get<WorkshopResult>(cacheKey);
            if (_config.EnableCaching && cache != null)
            {
                workshop = cache;
                ShowSkinGui(player, workshop, options);
                return;
            }
            else
            {
                DownloadWorkshop(workshop, () =>
                {
                    ShowSkinGui(player, workshop, options);
                }, cacheKey);
            }
        }

        void ShowSkinGui(BasePlayer player, WorkshopResult workshop, GuiOptions options)
        {
            CloseSkinGui(player);
            
            var container = new CuiElementContainer();

            var guiHelper = new GuiHelper(player);

            var panel = guiHelper.Panel("Overlay", 0.1, 0.9, 0.1, 0.9, "0.0 0.0 0.0 0.9", true);

            guiHelper.Label(panel,
                options.ShowSkinProfile != ""
                    ? workshop.GetItems()[0].Title
                    : Lang("GuiTitleSkinShop", player.UserIDString),
                0.01, 0.2, 0.94, 0.99
            );

            AddButtons(guiHelper, panel, player, workshop, options);

            if (options.ShowCategories)
            {
                CategoryButtons(player, guiHelper, panel, workshop);
            }else if (options.ShowSkinPacks)
            {
                SkinPacksGrid(guiHelper, panel, workshop, player);
            }
            else if (options.ShowSkinProfile != "")
            {
                SkinProfileGui(guiHelper, panel, workshop, player);
            }
            else
            {
                AddGrid(guiHelper, panel, player, workshop);
            }

            _skinGui[player.UserIDString] = guiHelper;
            guiHelper.Open();
        }

        private void AddButtons(GuiHelper guiHelper, string parent, BasePlayer player, WorkshopResult workshop, GuiOptions options)
        {
            var buttonbar = guiHelper.Panel(parent, 0.2, 0.99, 0.92, 0.97);
            
            guiHelper.Button(buttonbar, 
                Lang("GuiCloseButton", player.UserIDString), 
                "0.69 0.52 0.49 1.0",
                "skinshop.close",
                0.9, 1.0, 0.0, 1.0
            );

            guiHelper.Button(buttonbar, 
                Lang("GuiButtonMySkins", player.UserIDString), 
                workshop.OwnerId == player.UserIDString ? "0.42 0.55 0.22 1.0" : "0.23 0.23 0.23 1.0",
                "skinshop.myskins",
                0.64, 0.74, 0.0, 1.0
            );

            if (_config.SkinsPacksEnabled)
            {
                guiHelper.Button( buttonbar, 
                    Lang("GuiButtonSkinPacks", player.UserIDString), 
                    options.ShowSkinPacks ? "0.42 0.55 0.22 1.0" : "0.23 0.23 0.23 1.0",
                    "skinshop.buypacks",
                    0.53, 0.63, 0.0, 1.0
                );
            }
            
            guiHelper.Button(buttonbar, 
                Lang("GuiButtonSelectCategory", player.UserIDString), 
                workshop.Categories.Count == 0 ? "0.23 0.23 0.23 1.0" : "0.42 0.55 0.22 1.0",
                $"skinshop.selectcategory {workshop.Escape()}",
                0.75, 0.89, 0.0, 1.0
            );

            if (options.ShowSkinProfile == "" && !options.ShowSkinPacks)
            {
                GuiSearchBar(guiHelper, buttonbar, workshop);

                GuiPagination(guiHelper, buttonbar, workshop);
            }

        }

        private void GuiSearchBar(GuiHelper guiHelper, string parent, WorkshopResult workshop)
        {
            
            var inputpanel = guiHelper.Panel(parent, 0.0, 0.24, 0.0, 1.0, "0.0 0.0 0.0 1.0");

            guiHelper.container.Add(new CuiElement
            {
                Parent = inputpanel,
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Text = "",
                        IsPassword = false,
                        CharsLimit = 50,
                        Command = $"skinshop.search {workshop.Escape()}",
                        Align = TextAnchor.MiddleLeft
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.05 0.0",
                        AnchorMax = "0.95 1.0"
                    }
                }
            });
            
            var searchBtn = guiHelper.Panel( parent, 0.25, 0.29, 0.0, 1.0, "0.0 0.0 0.0 1.0");

            guiHelper.Image(searchBtn, 0.2, 0.8, 0.2, 0.8, GetImage(_config.SearchIconUrl));
        }

        private void GuiPagination(GuiHelper guiHelper, string parent, WorkshopResult workshop)
        {
            var pagination = guiHelper.Panel(parent, 0.3, 0.52, 0.0, 1.0);
            
            guiHelper.Button( pagination,
                "<<",
                "0.0 0.0 0.0 1.0",
                workshop.Page > 1 ? $"skinshop.page {workshop.Page - 1} {workshop.Escape()}" : "",
                0.0, 0.29, 0.0, 1.0
            );
            
            guiHelper.Button(pagination,
                string.Format("{0} of {1}",workshop.Page, workshop.TotalPages),
                "0.0 0.0 0.0 1.0",
                "",
                0.3, 0.7, 0.0, 1.0
            );
            
            guiHelper.Button( pagination,
                ">>",
                "0.0 0.0 0.0 1.0",
                workshop.Page < workshop.TotalPages ? $"skinshop.page {workshop.Page+1} {workshop.Escape()}" : "",
                0.71, 1.0, 0.0, 1.0
            );
        }
        
        private CuiRawImageComponent GetImage(string url, ulong imageId = 0)
        {
            CuiRawImageComponent img;
            if (ImageLibrary.Call<bool>("HasImage", url, imageId))
            {
                img = new CuiRawImageComponent()
                {
                    Png = ImageLibrary?.Call<string>("GetImage", url, imageId)
                };
            }
            else
            {
                ImageLibrary.Call<bool>("AddImage", url, url, imageId, ImageCallback(url));
                img = new CuiRawImageComponent()
                {
                    Url = url
                };
            }

            return img;
        }

        private void SkinPacksGrid(GuiHelper guiHelper, string parent, WorkshopResult workshop, BasePlayer player)
        {
            var grid = guiHelper.Panel( parent, 0.2, 0.8, 0.01, 0.89);

            foreach (var gridItem in GetGridItems(_config.SkinPacks.Count, 3, 2))
            {
                var itemPane = gridItem.Pane(guiHelper.container, grid);

                var packId = gridItem.Id;

                var pack = _config.SkinPacks[packId];
                
                guiHelper.Label( itemPane, pack.PackName, 0.0, 1.0, 0.85, 1.0, 14, "1.0 1.0 1.0 1.0",
                    TextAnchor.MiddleCenter);

                guiHelper.Image( itemPane, 0.0, 1.0, 0.15, 0.85, GetImage(pack.ImageUrl));
                
                guiHelper.Button( itemPane, 
                    Lang("GuiButtonBuyNowShort", player.UserIDString, pack.Price),
                    Color("Green"), 
                    $"skinshop.buypack {packId}",
                    0.0, 1.0, 0.0, 0.15);
            }
            
        }

        private void SkinProfileGui(GuiHelper guiHelper, string parent, WorkshopResult workshop, BasePlayer player)
        {
            if (workshop.GetItems().Count == 0) return;
            var item = workshop.GetItems()[0];

            var grid = guiHelper.Panel( parent, 0.01, 0.99, 0.01, 0.89,  "0.0 0.0 0.0 0.9");

            var imagebox = guiHelper.Panel( grid, 0.05, 0.3, 0.5, 0.95, "0.0 0.0 0.0 1.0");

            guiHelper.Image( imagebox, 0.01, 0.99, 0.01, 0.99, GetImage(item.Image));

            var instantSell = guiHelper.Panel( grid, 0.33, 0.6, 0.5, 0.95, "0.0 0.0 0.0 1.0");

            guiHelper.Label( instantSell, Lang("InstantSell", player.UserIDString), 0.05, 0.95, 0.8, 1.0, 16);

            guiHelper.Label( instantSell,
                Lang("InstantSellDescription", player.UserIDString,
                    item.GetInstantSellPrice(_config.DefaultInstantPricePrice)),
                0.05, 0.95, 0.2, 0.8, 12, "1.0 1.0 1.0 1.0", TextAnchor.UpperLeft);

            guiHelper.Button( instantSell,
                Lang("ButtonSellNow", player.UserIDString),
                Color("Green"),
                $"skinshop.sellskin {item.Id}",
                0.01, 0.99, 0.01, 0.2);
        }


        private List<GuiGridItem> GetGridItems(int totalItems, int columns, int minimumRows=0, double padding=0.01)
        {
            var grid = new List<GuiGridItem>();
            var rows = (int)Math.Max(minimumRows, Math.Ceiling((double)totalItems / columns));
            var totalWidth = 0.99;
            
            var itemWidth = (totalWidth / columns) - padding;
            var itemHeight = (totalWidth / rows) - padding;

            var col = 0;
            var row = 0;
            for (var i = 0; i < totalItems; i++)
            {
                var x = (col * itemWidth) + (padding * (col + 1));
                var y = ((rows - 1 - row) * itemHeight) + (padding * (rows - 1 - row)) + 0.01;
                grid.Add(new GuiGridItem
                {
                    Id = i,
                    x1 = x,
                    x2 = x + itemWidth,
                    y1 = y,
                    y2 = y + itemHeight
                });
                col++;
                if (col >= columns)
                {
                    col = 0;
                    row++;
                }
            }

            return grid;
        }

        private void AddGrid(GuiHelper guiHelper, string parent, BasePlayer player, WorkshopResult workshop)
        {
            var subtitle = workshop.OwnerId == player.UserIDString ? Lang("GuiButtonMySkins", player.UserIDString) :  workshop.Categories[0];
            GuiAddSubtitle(guiHelper, subtitle, parent);
            
            var grid = guiHelper.container.Add(new CuiPanel{
                RectTransform =
                {
                    AnchorMin = "0.01 0.01",
                    AnchorMax = "0.99 0.89"
                },
                Image =
                {
                    Color = "0.0 0.0 0.0 0.9"
                }
            }, parent);

            var items = workshop.GetItems();

            foreach (var gridItem in GetGridItems(items.Count, 6, 3))
            {
                var item = items[gridItem.Id];
                
                // Skip item if it is in the blacklist
                if (_config.BlackListedSkins.Contains(Convert.ToUInt64(item.Id))) continue;
                    
                var itemPane = gridItem.Pane(guiHelper.container, grid);

                guiHelper.Image( itemPane, 0.0, 1.0, 0.15, 0.85, GetImage(item.Image));

                guiHelper.Label( itemPane, item.Title, 0.05, 0.95, 0.85, 1.0, 12);

                if (workshop.OwnerId == "" && permission.UserHasPermission(player.UserIDString, blacklistPermission))
                {
                    var blacklistbtn = guiHelper.Button( itemPane, "", Color("Red"), $"skinshop.blacklist.id {item.Id} {workshop.Escape()}", 0.85,
                        1.0, 0.86, 0.99, 10);
                    
                    guiHelper.Image( blacklistbtn, 0.1, 0.9, 0.05, 0.95, GetImage(_config.BlacklistIconUrl));
                }

                var playerItem = PlayerOwnsItem(player, item);
                if (playerItem != null)
                {
                    if((workshop.OwnerId != "" || !permission.UserHasPermission(player.UserIDString, blacklistPermission)) && PlayerHasItem(player, item.Shortname)){
                        guiHelper.Button( itemPane, Lang("ButtonApply", player.UserIDString), Color("Red"), $"skinshop.apply {item.Escape()}", 0.80,
                            1.0, 0.86, 0.99, 10);
                    }
                    if (playerItem.Equipped)
                    {
                        guiHelper.Button( itemPane,
                            Lang("GuiButtonSkinEquipped", player.UserIDString),
                            Color("Green"),
                            $"skinshop.unequip {workshop.Escape()} {item.Escape()}",
                            0.01, 0.55, 0.0, 0.13, 12);
                    }
                    else
                    {
                        guiHelper.Button( itemPane,
                            Lang("GuiButtonSkinUnequipped", player.UserIDString),
                            Color("Grey"),
                            $"skinshop.equip {workshop.Escape()} {item.Escape()}",
                            0.01, 0.55, 0.0, 0.13, 12);
                    }

                    guiHelper.Button( itemPane, 
                        Lang("ButtonSell", player.UserIDString), 
                        Color("Blue"),
                        $"skinshop.skinprofile {player.UserIDString} {item.Id}",
                        0.56, 0.99, 0.0, 0.13, 12);
                }
                else
                {
                    guiHelper.Button( itemPane,
                        Lang("GuiButtonBuyNow", player.UserIDString, item.GetPrice(_config.DefaultSkinPrice, GetDiscount(player))),
                        "0.22 0.35 0.55 1.0",
                        $"skinshop.buy {workshop.Escape()} {item.Escape()}",
                        0.01, 0.99, 0.0, 0.13, 12);
                }
            }
        }

        private void CategoryButtons(BasePlayer player, GuiHelper guiHelper, string parent, WorkshopResult workshop)
        {
            GuiAddSubtitle(guiHelper, "Select a Category", parent);

            var grid = guiHelper.Panel( parent, 0.01, 0.99, 0.01, 0.89, "0.0 0.0 0.0 0.9");
            
            var categories = _categories.Where(x => !_config.HideCategories.Contains(x.Value));

            foreach (var gridItem in GetGridItems(categories.Count(), 6, 0, 0.005))
            {
                var categoryItem = categories.Skip(gridItem.Id).First();

                var btn = guiHelper.Button( grid,
                    Lang(categoryItem.Value, player.UserIDString),
                    workshop.Categories.Contains(categoryItem.Key) ? "0.42 0.55 0.22 1.0" : "0.0 0.0 0.0 1.0",
                    $"skinshop.category {workshop.Escape()} {Uri.EscapeDataString(categoryItem.Key)}",
                    gridItem.x1, gridItem.x2, gridItem.y1, gridItem.y2, 12);
                
                if (_config.EnabledCategoryIcons && _categoryIconsLoaded)
                {
                    guiHelper.Image( btn, 0.02, 0.17, 0.05, 0.95, GetImage(categoryItem.Value));
                }
            }
        }

        private void GuiAddSubtitle(GuiHelper guiHelper, string text, string parent)
        {
            guiHelper.Label( parent, text, 0.01, 0.2, 0.9, 0.94, 16);
        }
        
        #endregion
        
        #region Skin Functions

        private void ApplySkin(BasePlayer player, WorkShopItem item)
        {
            foreach (var inventoryItem in player.inventory.AllItems())
            {
                if (inventoryItem.info.shortname == item.Shortname)
                {
                    SetSkinId(inventoryItem, Convert.ToUInt64(item.Id));
                }
            }
        }
        
        private void SetSkin(BasePlayer player, Item item)
        {
            if (item.skin != 0UL) return;
            
            var e = EquippedItem(player, item.info.shortname);
            if (e != null)
            {
                var skinID = Convert.ToUInt64(e.Id);
                SetSkinId(item, skinID);
            }
        }

        private void SetSkinId(Item item, ulong skinID)
        {
            item.skin = skinID;
            var held = item.GetHeldEntity();
            if (held != null)
            {
                held.skinID = skinID;
                held.SendNetworkUpdate();
            }

            var world = item.GetWorldEntity();
            if (world != null)
            {
                world.skinID = skinID;
                world.SendNetworkUpdate();
            }
            item.MarkDirty();
        }

        [CanBeNull]
        private string BuyItem(BasePlayer player, WorkShopItem item)
        {
            if (!HasEconomicsPlugin())
                return Lang("PluginNotInstalled", player.UserIDString, _config.CurrencyPlugin);
            
            var balance = GetBalance(player.UserIDString);
            if (balance < item.GetPrice(_config.DefaultSkinPrice, GetDiscount(player))) return Lang("NotEnoughCoinsToBuy", player.UserIDString, item.GetPrice(_config.DefaultSkinPrice, GetDiscount(player)));

            // Take money
            if (!TakeFunds(player.UserIDString, item.GetPrice(_config.DefaultSkinPrice, GetDiscount(player))))
            {
                return Lang("WithdrawCoinsFailed",player.UserIDString);
            }

            GiveItem(player, item);
            return null;

        }

        private string BuyPack(BasePlayer player, SkinPack pack)
        {
            var spaceAvailable = PlayerInventorySpaceAvailable(player);
            if (spaceAvailable <= 0)
            {
                return Lang("NoRoomInInventory",player.UserIDString);
            }
            
            if (!HasEconomicsPlugin())
                return Lang("PluginNotInstalled", player.UserIDString, _config.CurrencyPlugin);
            
            var balance = GetBalance(player.UserIDString);
            if (balance < pack.Price) return string.Format("You need {0} to buy this pack!", pack.Price);

            // Take money
            if (!TakeFunds(player.UserIDString, pack.Price))
            {
                return Lang("WithdrawCoinsFailed",player.UserIDString);
            }

            GiveSkinPack(player, pack);

            return null;
        }

        private void GiveItem(BasePlayer player, WorkShopItem item, bool equip = true)
        {
            if (!_ownedItems.ContainsKey(player.UserIDString))
            {
                _ownedItems.Add(player.UserIDString, new List<WorkShopItem>());
            }
            
            // Disallow duplicates?
            if (PlayerOwnsItem(player, item) != null)
            {
                return;
            }

            if (equip)
            {
                var e = EquippedItem(player, item.Shortname);
                if (e != null) e.Equipped = false;
                item.Equipped = true;
                item.Wrapped = false;
            }

            _ownedItems[player.UserIDString].Add(item);
            NeedsWriting(player.UserIDString);
        }


        private void EquipItem(BasePlayer player, WorkShopItem item)
        {
            var e = EquippedItem(player, item.Shortname);
            if(e != null) e.Equipped = false;

            var owned = PlayerOwnsItem(player, item);
            if (owned != null) owned.Equipped = true;
            NeedsWriting(player.UserIDString);
        }

        private void UnEquipItem(BasePlayer player, WorkShopItem item)
        {
            var owned = PlayerOwnsItem(player, item);
            if (owned != null) owned.Equipped = false;
            NeedsWriting(player.UserIDString);
        }

        private bool HasEconomicsPlugin()
        {
            if (_config.CurrencyPlugin == "Economics")
            {
                return Economics != null && Economics.IsLoaded;
            }else if (_config.CurrencyPlugin == "ServerRewards")
            {
                return ServerRewards != null && ServerRewards.IsLoaded;
            }

            return false;
        }

        private bool AddFunds(string playerId, double amount)
        {
            if (_config.CurrencyPlugin == "ServerRewards")
            {
                return ServerRewards.Call<bool>("AddPoints", playerId, Convert.ToInt32(amount));
            }
            return Economics.Call<bool>("Deposit", playerId, amount);
        }

        private double GetBalance(string playerId)
        {
            if (_config.CurrencyPlugin == "ServerRewards")
            {
                return Convert.ToDouble(ServerRewards.Call<int>("CheckPoints", playerId));
            }
            return Economics.Call<double>("Balance", playerId);
        }

        private bool TakeFunds(string playerId, double amount)
        {
            if (_config.CurrencyPlugin == "ServerRewards")
            {
                return ServerRewards.Call<bool>("TakePoints", playerId, Convert.ToInt32(amount));
            }
            return Economics.Call<bool>("Withdraw", playerId, amount);
        }

        [CanBeNull]
        private WorkShopItem PlayerOwnsItem(BasePlayer player, WorkShopItem item)
        {
            if (!_ownedItems.ContainsKey(player.UserIDString)) return null;

            foreach (var i in _ownedItems[player.UserIDString])
            {
                if (i.Id == item.Id) return i;
            }

            return null;
        }

        private bool PlayerHasItem(BasePlayer player, string item)
        {
            foreach (var i in player.inventory.AllItems())
            {
                if (i.info.shortname == item) return true;
            }
            return false;
        }

        [CanBeNull]
        private WorkShopItem EquippedItem(BasePlayer player, string categoryShort)
        {
            if (!_ownedItems.ContainsKey(player.UserIDString)) return null;
            
            foreach (var i in _ownedItems[player.UserIDString])
            {
                if (i.Shortname == categoryShort && i.Equipped) return i;
            }
            return null;
        }

        #endregion

        #region Skin Pack Functions

        private bool IsSkinPack(Item item)
        {
            if (item == null || item.info.shortname != "wrappedgift") return false;
            
            foreach (var pack in _config.SkinPacks)
            {
                if (item.name == pack.Value.PackName) return true;
            }
            return false;
        }

        private SkinPack GetSkinPack(Item item)
        {
            foreach (var pack in _config.SkinPacks)
            {
                if (item.name == pack.Value.PackName) return pack.Value;
            }

            return null;
        }
        
        private void SetupSkinPack(Item item)
        {
            if (!_config.ConvertGiftsToPacks) return;
            
            if (item != null && item.info.shortname == "wrappedgift" && !IsSkinPack(item))
            {
                var pack = _config.SkinPacks[0];
                item.name = pack.PackName;
                item.skin = pack.SkinId;
            }
        }
        
        private List<WorkShopItem> OpenPack(BasePlayer player, SkinPack pack)
        {
            var items = new List<WorkShopItem>();

            // Filter out blacklisted skins
            var database = _skinDatabase.Where(x => !_config.BlackListedSkins.Contains(Convert.ToUInt64(x.Key)) 
                                                    && (pack.PossibleSkinIds.Length == 0 || pack.PossibleSkinIds.Contains(Convert.ToUInt64(x.Key))) 
                                                    && (pack.PossibleSkinCategories.Length == 0 || pack.PossibleSkinCategories.Contains(x.Value.Shortname))).Select(x => x.Key).ToArray();
            
            Random random = new Random();
            database = database.OrderBy(x => random.Next()).Take(pack.NumberOfSkins).ToArray();
            foreach (var i in database)
            {
                items.Add(_skinDatabase[i]);
                GiveItem(player, _skinDatabase[i], false);
            }
            
            PostToDiscord(Lang("DiscordOpenPackMessage", player.UserIDString, GetServerTime(), player.displayName, pack.PackName));

            return items;
        }

        private void GiveSkinPack(BasePlayer player, SkinPack pack)
        {
            var item = ItemManager.Create(ItemManager.FindItemDefinition("wrappedgift"), 1, pack.SkinId);
            item.name = pack.PackName;
            var beltAvailable = player.inventory.containerBelt.capacity -
                                player.inventory.containerBelt.itemList.Count;
            if (beltAvailable > 0)
            {
                item.MoveToContainer(player.inventory.containerBelt);
            }
            else
            {
                player.inventory.GiveItem(item);
            }
        }

        private string SellSkin(BasePlayer player, string itemId)
        {
            if (!HasEconomicsPlugin())
                return Lang("PluginNotInstalled", player.UserIDString, _config.CurrencyPlugin);
            
            if (!_ownedItems.ContainsKey(player.UserIDString))
            {
                return Lang("InvalidPlayer", player.UserIDString);
            }

            for (int i = _ownedItems[player.UserIDString].Count - 1; i >= 0; i--)
            {
                if (_ownedItems[player.UserIDString][i].Id == itemId)
                {
                    var price = _ownedItems[player.UserIDString][i]
                        .GetInstantSellPrice(_config.DefaultInstantPricePrice);
                    _ownedItems[player.UserIDString].RemoveAt(i);

                    AddFunds(player.UserIDString, price);
                    NeedsWriting(player.UserIDString);
                    return Lang("ItemSold", player.UserIDString, price);
                }
            }

            return Lang("ItemNotFound", player.UserIDString);
        }

        #endregion
        
        #region Open Packs GUI
        
        void ShowPacksGui(BasePlayer player, List<WorkShopItem> items)
        {
            CloseSkinGui(player);

            var guiHelper = new GuiHelper(player);

            var panel = guiHelper.Panel("Overlay", 0.0, 1.0, 0.0, 1.0, "0.17 0.17 0.17 1.0", true);

            var buttonbar = guiHelper.Panel(panel, 0.3, 1.0, 0.9, 1.0);

            guiHelper.Button(buttonbar, Lang("GuiCloseButton", player.UserIDString),
                "0.69 0.52 0.49 1.0",
                "skinshop.packsclose",
                0.8, 1.0, 0.0, 1.0);

            guiHelper.Button(buttonbar, Lang("GuiButtonViewAllMySkins", player.UserIDString),
                Color("Green"),
                "skinshop.myskins",
                0.5, 0.78, 0.0, 1.0
            );

            guiHelper.Label(panel, Lang("GuiTitleOpenSkinPack", player.UserIDString), 0.01, 0.25, 0.9, 0.99, 30);

            var grid = guiHelper.Panel(panel, 0.15, 0.85, 0.05, 0.85, "1.0 1.0 1.0 0.0");

            foreach (var gridItem in GetGridItems(items.Count, 3, 2))
            {
                var item = items[gridItem.Id];

                var bg = guiHelper.Image(grid, gridItem.x1, gridItem.x2, gridItem.y1, gridItem.y2,
                    GetImage(_config.SkinPackImageUrl));
                
                if (item.Wrapped)
                {
                    guiHelper.Button(bg, "", "0.0 0.0 0.0 0.0",
                        $"skinshop.packsopen  {Uri.EscapeDataString(JsonConvert.SerializeObject(items))} {item.Id}",
                        0.0, 1.0, 0.0, 1.0);
                }
                else
                {
                    guiHelper.Image(bg, 0.075, 0.925, 0.15, 0.85, GetImage(item.Image));

                    var label = guiHelper.Panel(bg, 0.075, 0.925, 0.075, 0.15, "0.0 0.0 0.0 1.0");

                    guiHelper.Label(label, item.Title, 0.0, 1.0, 0.0, 1.0, 14, "1.0 1.0 1.0 1.0", TextAnchor.MiddleCenter);

                    var categorylabel = guiHelper.Panel(bg, 0.075, 0.925, 0.85, 0.925, "0.0 0.0 0.0 1.0");

                    guiHelper.Label(categorylabel, item.Category, 0.0, 1.0, 0.0, 1.0, 14, "1.0 1.0 1.0 1.0", TextAnchor.MiddleCenter);
                }
            }


            _skinGui[player.UserIDString] = guiHelper;
            guiHelper.Open();
        }
        

        #endregion
        
        #region Data Management
        
        private void LogMsg(string msg, string level="debug")
        {
            if (level == "error" || _config.LogLevel == "debug" || (_config.LogLevel == "info" && level == "info"))
            {
                Puts(msg);
            }
        }

        private void DownloadDatabase(IPlayer iplayer)
        {
            webrequest.Enqueue(_downloadJsonUrl, "", (code, downloadString) =>
            {
                try
                {
                    var skindb = JObject.Parse(downloadString);
                    if (skindb.Count == 0)
                    {
                        LogMsg(Lang("DownloadEmpty"), "error");
                        return;
                    }
                    
                    Interface.Oxide.DataFileSystem.WriteObject("SkinShop\\database", skindb);
                    LoadDatabase();
                    iplayer.Reply(Lang("SkinDatabaseLoaded", iplayer.Id, _skinDatabase.Count));
                }
                catch (Exception e)
                {
                    LogMsg(Lang("DownloadFailed",null, e.Message), "error");
                }
            }, this);
        }
        
        private void DownloadWorkshop(WorkshopResult workshop, Action callback, string cacheKey="")
        {
            webrequest.Enqueue(workshop.GetUrl(), "", (code, downloadString) =>
            {
                Match totalReg = Regex.Match(downloadString, @"of ([\d,]+) entries");
                if (!totalReg.Success) return;

                var total = Convert.ToInt32(totalReg.Groups[1].Value.Replace(",",""));
                if (total <= 0) return;

				workshop.TotalResults = total;
				if (_config.DefaultSkinsCountPerItem > 0)
				{
					if (total > _config.DefaultSkinsCountPerItem)
					{
						workshop.TotalResults = _config.DefaultSkinsCountPerItem;
					}
				}                
            
                Regex regex = new Regex("<div class=\\\"workshopItem\\\">.*?</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

                var matches = regex.Matches(
                    downloadString);
        
                foreach (Match match in matches)
                {
                    try
                    {
                        Match titleReg = Regex.Match(match.Value, "<div class=\\\"workshopItemTitle[^\\\"]*\\\">(.*?)</div>");
                        if (!titleReg.Success) continue;

                        Match infoReg = Regex.Match(match.Value, "SharedFileBindMouseHover\\(.*?({.*?}).*?\\)");
                        if (!infoReg.Success) continue;

                        var info = JsonConvert.DeserializeObject<JObject>(infoReg.Groups[1].Value);
                
                        Match imgReg = Regex.Match(match.Value,
                            "<img class=\\\"workshopItemPreviewImage[^\\\"]*\\\" src=\\\"([^\\\"]*)\\\"");
                        if (!imgReg.Success) continue;

                        var item = new WorkShopItem()
                        {
                            Title = CleanInput(info.Value<string>("title")),
                            Id = info.Value<string>("id"),
                            Description = CleanInput(info.Value<string>("description")),
                            Image = imgReg.Groups[1].Value.Split('?')[0],
                            Category = workshop.Categories[0]
                        };

                        workshop.AddItem(item);
                        if (!_skinDatabase.ContainsKey(item.Id))
                        {
                            _skinDatabase.Add(item.Id, item);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }

                if (cacheKey != "")
                {
                    MemoryCache.Add(cacheKey, workshop, new MemoryCache.CacheItemOptions()
                    {
                        AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(24)
                    });
                }

                callback();
            }, this);
        }
        
        private void SaveData()
        {
            LogMsg(Lang("SavingDataMessage", null, _needsWriting.Count), "info");
            foreach (var playerId in _needsWriting)
            {
                if (!_ownedItems.ContainsKey(playerId)) continue;
                Interface.Oxide.DataFileSystem.WriteObject("SkinShop\\"+playerId+"_items", _ownedItems[playerId]);
            }
            _needsWriting.Clear();
        }

        private void SaveSkinDatabase()
        {
            
            Interface.Oxide.DataFileSystem.WriteObject("SkinShop\\database", _skinDatabase);
        }

        private void LoadData()
        {
            try
            {
                foreach (string file in Interface.Oxide.DataFileSystem.GetFiles("SkinShop","*_items.json"))
                {
                    Match match = Regex.Match(file,  @".*[/\\]{1}([\d]+)_items");
                    LogMsg(file, "debug");
                    if (match.Success)
                    {
                        var uid = match.Groups[1].Value;
                        LogMsg(Lang("SkinsLoadedForUser", null, uid), "debug");
                        var jsonData = Interface.Oxide.DataFileSystem.ReadObject<List<WorkShopItem>>("SkinShop\\"+uid+"_items");
                        LogMsg(JsonConvert.SerializeObject(jsonData), "debug");
                        _ownedItems[uid] = jsonData;
                    }
                    else
                    {
                        LogMsg(Lang("UserIDMatchNotFound"),"debug");
                    }
                }
            }
            catch (Exception e)
            {
                LogMsg(e.Message, "error");
                LogMsg(Lang("FilesNotFound"), "info");
            }

            
        }

        private void LoadDatabase()
        {
            try
            {
                _skinDatabase =
                    Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, WorkShopItem>>("SkinShop\\database");
                LogMsg(Lang("SkinDatabaseLoaded", null, _skinDatabase.Count), "info");
            }
            catch (Exception e)
            {
                
            }
        }
        
        private Action ImageCallback(string imageName, ulong imageId=0)
        {
            return () =>
            {
                if(!_config.AutomaticallyRemoveMissingImages) return;
                
                if (!ImageLibrary.Call<bool>("HasImage", imageName, imageId))
                {
                    LogMsg(Lang("MissingImage",null, imageName), "error");
                    List<string> deleting = new List<string>();
                    foreach (var delete in _skinDatabase.Where(x => x.Value.Image == imageName))
                    {
                        deleting.Add(delete.Key);
                    }

                    foreach (var d in deleting)
                    {
                        LogMsg(Lang("ItemDeleted",null, _skinDatabase[d].Title), "error");
                        _skinDatabase.Remove(d);
                    }

                    SaveSkinDatabase();
                }
            };
        }

        private Action CategoryIconsDone(int count)
        {
            return () =>
            {
                LogMsg(Lang("IconsLoaded",null, count), "info");
                if(count > 0) _categoryIconsLoaded = true;
            };
        }
        
        private void LoadIconImages()
        {
            if (!_config.EnabledCategoryIcons || ImageLibrary == null || !ImageLibrary.IsLoaded) return;

            var itemIcons = new List<KeyValuePair<string, ulong>>();
            foreach (var cat in _categories)
            {
                itemIcons.Add(new KeyValuePair<string, ulong>(cat.Value,0UL));
            }

            ImageLibrary.Call("LoadImageList", "SkinShop", itemIcons, CategoryIconsDone(itemIcons.Count));
        }

        private void NeedsWriting(string playerId)
        {
            if (!_needsWriting.Contains(playerId))
            {
                _needsWriting.Add(playerId);
            }
        }
        #endregion

        #region Commands

        
        [Command("skinshop"), Permission(useSkinshopPermission)]
        private void ShowSkinShop(IPlayer iplayer, string command, string[] args)
        {
            if (_config.ShowCategorySelectLanding)
            {
                SelectCategorySkinShop(iplayer, command, args);
                return;
            }
            PrepareShowSkinGui(iplayer.Object as BasePlayer, new WorkshopResult());
        }

        
        [Command("skinshop.close"), Permission(useSkinshopPermission)]
        private void CloseSkinShop(IPlayer iplayer, string command, string[] args)
        {
            CloseSkinGui(iplayer.Object as BasePlayer);
        }
        
        [Command("skinshop.page"), Permission(useSkinshopPermission)]
        private void PageChangeSkinShop(IPlayer iplayer, string command, string[] args)
        {
            var workshop = UnEscape(args[1]);
            workshop.Page = Convert.ToInt32(args[0]);
            PrepareShowSkinGui(iplayer.Object as BasePlayer, workshop);
        }
        
        [Command("skinshop.search"), Permission(useSkinshopPermission)]
        private void SearchSkinShop(IPlayer iplayer, string command, string[] args)
        {
            var workshop = UnEscape(args[0]);
            var search = "";
            if (args.Length > 1)
            {
                search = string.Join(" ", args.Skip(1));
            }

            workshop.Search = search;

            PrepareShowSkinGui(iplayer.Object as BasePlayer, workshop);
        }
        
        [Command("skinshop.category"), Permission(useSkinshopPermission)]
        private void CategorySkinShop(IPlayer iplayer, string command, string[] args)
        {
            var workshop = UnEscape(args[0]);
            var category = Uri.UnescapeDataString(args[1]);
            workshop.Categories = new List<string> {category};
            workshop.Page = 1;
            workshop.Search = "";

            PrepareShowSkinGui(iplayer.Object as BasePlayer, workshop);
        }
        
        [Command("skinshop.selectcategory"), Permission(useSkinshopPermission)]
        private void SelectCategorySkinShop(IPlayer iplayer, string command, string[] args)
        {
            WorkshopResult workshop;
            if (args.IsEmpty())
            {
                workshop = new WorkshopResult();
            }
            else
            {
                workshop = UnEscape(args[0]);
                workshop.RemoveOwner();
            }

            PrepareShowSkinGui(iplayer.Object as BasePlayer, workshop, new GuiOptions
            {
                ShowCategories = true
            });
        }
        [Command("skinshop.buy"), Permission(useSkinshopPermission)]
        private void BuySkinShop(IPlayer iplayer, string command, string[] args)
        {
            var workshop = UnEscape(args[0]);
            var item = JsonConvert.DeserializeObject<WorkShopItem>(Uri.UnescapeDataString(args[1]));
            var buy = BuyItem(iplayer.Object as BasePlayer, item);
            if (buy != null)
            {
                ((BasePlayer)iplayer.Object).ChatMessage(buy);
            }
            PrepareShowSkinGui(iplayer.Object as BasePlayer, workshop);
        }
        
        [Command("skinshop.equip"), Permission(useSkinshopPermission)]
        private void EquipItemCommand(IPlayer iplayer, string command, string[] args)
        {
            var workshop = UnEscape(args[0]);
            var item = JsonConvert.DeserializeObject<WorkShopItem>(Uri.UnescapeDataString(args[1]));
            EquipItem(iplayer.Object as BasePlayer, item);
            PrepareShowSkinGui(iplayer.Object as BasePlayer, workshop);
        }
        
        [Command("skinshop.unequip"), Permission(useSkinshopPermission)]
        private void UnEquipItemCommand(IPlayer iplayer, string command, string[] args)
        {
            var workshop = UnEscape(args[0]);
            var item = JsonConvert.DeserializeObject<WorkShopItem>(Uri.UnescapeDataString(args[1]));
            UnEquipItem(iplayer.Object as BasePlayer, item);
            PrepareShowSkinGui(iplayer.Object as BasePlayer, workshop);
        }
        
        [Command("skinshop.myskins"), Permission(useSkinshopPermission)]
        private void MySkinsPage(IPlayer iplayer, string command, string[] args)
        {
            var workshop = new WorkshopResult();
            workshop.OwnerId = ((BasePlayer) iplayer.Object).UserIDString;
            
            PrepareShowSkinGui(iplayer.Object as BasePlayer, workshop);
        }
        
        [Command("skinshop.buypacks"), Permission(useSkinshopPermission)]
        private void BuyPacksPage(IPlayer iplayer, string command, string[] args)
        {
            if (!_config.SkinsPacksEnabled) return;
            
            var workshop = new WorkshopResult();

            PrepareShowSkinGui(iplayer.Object as BasePlayer, workshop, new GuiOptions
            {
                ShowSkinPacks = true
            });
        }

        [Command("skinshop.packsopen"), Permission(useSkinshopPermission)]
        private void OpenSkinPacksShop(IPlayer iplayer, string command, string[] args)
        {
            if (!_config.SkinsPacksEnabled) return;
            
            var items = JsonConvert.DeserializeObject<List<WorkShopItem>>(Uri.UnescapeDataString(args[0]));
            if (args[1] != null)
            {
                foreach (var i in items)
                {
                    if (i.Id == args[1])
                    {
                        i.Wrapped = false;
                    }
                }
            }

            ShowPacksGui(iplayer.Object as BasePlayer, items);
        }
        
        [Command("skinshop.packsclose"), Permission(useSkinshopPermission)]
        private void CloseSkinPacksShop(IPlayer iplayer, string command, string[] args)
        {
            CloseSkinGui(iplayer.Object as BasePlayer);
        }
        
        [Command("skinshop.skinprofile"), Permission(useSkinshopPermission)]
        private void ViewSkinProfileCommand(IPlayer iplayer, string command, string[] args)
        {
            if (args.Length < 2 || args[0] != iplayer.Id)
            {
                iplayer.Reply(Lang("AccessDenied", iplayer.Id));
                return;
            }
            PrepareShowSkinGui(iplayer.Object as BasePlayer, new WorkshopResult
            {
                OwnerId = args[0]
            }, new GuiOptions
            {
                ShowSkinProfile = args[1],
            });
        }

        private Timer ti;

        private void KillTimer()
        {
            ti.Destroy();
        }

        [Command("skinshop.download"), Permission(adminPermission)]
        private void DownloadSkinData(IPlayer iplayer, string command, string[] args)
        {
            DownloadDatabase(iplayer);
        }
        
        [Command("skinshop.builddatabase"), Permission(adminPermission)]
        private void BuildSkinData(IPlayer iplayer, string command, string[] args)
        {
            iplayer.Reply(Lang("CommandDatabaseBuilding", iplayer.Id));

            var categories = _categories.Keys.ToArray();
            var category = "";
            var cID = 0;
            var page = 1;
            if (args.Length > 0)
            {
                if (!Int32.TryParse(args[0], out cID))
                {
                    if (_categories.ContainsValue(args[0]))
                    {
                        category = _categories.FirstOrDefault(x => x.Value == args[0]).Key;
                    }
                }
            }

            if(args.Length > 1) page = Convert.ToInt32(args[1]);
            
            ti = timer.Every(2f, () =>
            {
                if (cID >= categories.Length)
                {
                    iplayer.Reply(Lang("DatabaseBuildCompleted", iplayer.Id));
                    KillTimer();
                    return;
                }
                var workshop = new WorkshopResult();
                workshop.Categories.Add(category != "" ? category : categories[cID]);
                workshop.Page = page;
                DownloadWorkshop(workshop, () =>
                {
                    iplayer.Reply(category != "" ? Lang("DatabaseCategoryPageDone", iplayer.Id, category, page) : Lang("DatabasePageDone", iplayer.Id, categories[cID], cID, page));
                    if (workshop.Page < workshop.TotalPages)
                    {
                        page++;
                    }
                    else
                    {
                        cID++;
                        page = 1;
                        SaveSkinDatabase();
                        if (category != "")
                        {
                            iplayer.Reply(Lang("DatabaseCategoryCompleted",iplayer.Id, category));
                            KillTimer();
                            return;
                        }
                    }
                });
            });
        }

        [Command("skinshop.givepack"), Permission(adminPermission)]
        private void GivePackCommand(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player;
            ulong playerid;
            if (UInt64.TryParse(args[0], out playerid)){
                player = BasePlayer.FindByID(playerid);
            }
            else
            {
                iplayer.Reply("Invalid player");
                return;
            }

            GiveSkinPack(player, _config.SkinPacks[0]);
            iplayer.Reply($"Skin pack given to {player.displayName}");
        }
        
        [Command("skinshop.buypack")]
        private void BuyPackCommand(IPlayer iplayer, string command, string[] args)
        {
            var pack = _config.SkinPacks[Convert.ToInt32(args[0])];
            var buy = BuyPack(iplayer.Object as BasePlayer, pack);
            if (buy != null)
            {
                ((BasePlayer)iplayer.Object).ChatMessage(buy);
                return;
            }
            CloseSkinGui((BasePlayer)iplayer.Object);
        }
        
        [Command("skinshop.sellskin")]
        private void SellSkinCommand(IPlayer iplayer, string command, string[] args)
        {
            if (args.Length == 0) return;
            
            var result = SellSkin(iplayer.Object as BasePlayer, args[0]);
            iplayer.Message(result);
            CloseSkinGui((BasePlayer)iplayer.Object);
            // Open the my skins GUI
            MySkinsPage(iplayer, command, args);

        }
        
        [Command("skinshop.apply"), Permission(useSkinshopPermission)]
        private void ApplySkinCommand(IPlayer iplayer, string command, string[] args)
        {
            var item = UnEscapeItem(args[0]);
            ApplySkin(iplayer.Object as BasePlayer, item);
            iplayer.Message("Skin has been applied");
        }
        
        [Command("skinshop.getskin"), Permission(blacklistPermission)]
        private void GetSkinCommand(IPlayer iplayer, string command, string[] args)
        {
            var item = GetLookingAtItem(iplayer.Object as BasePlayer);
            if (item == null)
            {
                iplayer.Reply(Lang("NotLookingAtItem", iplayer.Id));
            }else if(item.skin == 0UL)
            {
                iplayer.Reply(Lang("ItemNoSkin", iplayer.Id, item.info.displayName.english));
            }
            else
            {
                iplayer.Reply(Lang("SkinIdIs",iplayer.Id, item.info.displayName.english, item.skin));
            }
        }
        
        [Command("skinshop.blacklist.id"), Permission(blacklistPermission)]
        private void BlacklistListId(IPlayer iplayer, string command, string[] args)
        {
            if (args.Length < 1 || !_skinDatabase.ContainsKey(args[0]))
            {
                iplayer.Reply(Lang("InvalidSkinId",iplayer.Id));
                return;
            }

            ulong id;

            if (!ulong.TryParse(args[0], out id))
            {
                iplayer.Reply(Lang("InvalidSkinId",iplayer.Id));
                return;
            }
            
            BlacklistSkin(id);
            iplayer.Message(Lang("SkinBlackListed",iplayer.Id,_skinDatabase[args[0]].Category));

            if (args.Length > 1 && args[1] != "")
            {
                var workshop = UnEscape(args[1]);
                PrepareShowSkinGui(iplayer.Object as BasePlayer, workshop);
            }
            
        }
        
        [Command("skinshop.blacklist.this"), Permission(blacklistPermission)]
        private void BlacklistListThis(IPlayer iplayer, string command, string[] args)
        {
            var item = GetLookingAtItem(iplayer.Object as BasePlayer);
            if (item != null && item.skin != 0UL)
            {
                SetSkinId(item, 0UL);
                BlacklistSkin(item.skin);
                iplayer.Reply(Lang("SkinBlackListed",iplayer.Id,item.info.displayName.english));
                return;
            }
            iplayer.Reply(Lang("MustLookAtCustomSkin",iplayer.Id));
            
        }
        #endregion
        
        #region GuiHelpers
        
        private class GuiGridItem
        {
            public int Id;
            public double x1;
            public double x2;
            public double y1;
            public double y2;
            
            public string Pane(CuiElementContainer container, string parent, string color="0.0 0.0 0.0 1.0")
            {
                return container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = AnchorMin(),
                        AnchorMax = AnchorMax()
                    },
                    Image =
                    {
                        Color = color
                    }
                }, parent);
            }

            public string AnchorMin()
            {
                return Convert.ToString(x1) + " " + Convert.ToString(y1);
            }

            public string AnchorMax()
            {
                return Convert.ToString(x2) + " " + Convert.ToString(y2);
            }
        }
        
        private class GuiHelper
        {
            public CuiElementContainer container = new CuiElementContainer();
            public BasePlayer player;

            private List<string> _elements = new List<string>();
            
            public GuiHelper(BasePlayer baseplayer)
            {
                player = baseplayer;
            }

            public string Button(string parent, string text, string buttonColor, string command, double x1, double x2, double y1, double y2, int fontSize=14, string textColor="1.0 1.0 1.0 1.0", TextAnchor align = TextAnchor.MiddleCenter)
            {
                var name = container.Add(new CuiButton
                {
                    Text =
                    {
                        Text = text,
                        Align = align,
                        Color = textColor,
                        FontSize = fontSize
                    },
                    Button =
                    {
                        Color = buttonColor,
                        Command = command
                    
                    },
                    RectTransform =
                    {
                        AnchorMin = x1+" "+y1,
                        AnchorMax = x2+" "+y2
                    }
                }, parent);
                _elements.Add(name);
                return name;
            }

            public string Panel(string parent, double x1, double x2, double y1, double y2, string color="0.0 0.0 0.0 0.0", bool cursor=false)
            {
                var name = container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = x1+" "+y1,
                        AnchorMax = x2+" "+y2
                    },
                    Image =
                    {
                        Color = color 
                    },
                    CursorEnabled = cursor
                }, parent);
                _elements.Add(name);
                return name;
            }

            public string Label(string parent, string text, double x1, double x2, double y1, double y2, int fontSize=22, string textColor="1.0 1.0 1.0 1.0", TextAnchor align = TextAnchor.MiddleLeft)
            {
                var name = container.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = text,
                        Align = align,
                        Color = textColor,
                        FontSize = fontSize
                    },
                    RectTransform =
                    {
                        AnchorMin = x1+" "+y1,
                        AnchorMax = x2+" "+y2
                    }
                }, parent);
                _elements.Add(name);
                return name;
            }

            public string Image(string parent, double x1, double x2, double y1, double y2, CuiRawImageComponent image)
            {
                var name = container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = x1+" "+y1,
                        AnchorMax = x2+" "+y2
                    },
                    Image = null,
                    RawImage = image
                }, parent);
                _elements.Add(name);
                return name;
            }

            public void Open()
            {
                CuiHelper.AddUi(player, container);
            }

            public int Close()
            {
                _elements.Reverse();
                foreach (var e in _elements)
                {
                    CuiHelper.DestroyUi(player, e);
                }

                return _elements.Count;
            }
        }
        #endregion
        
        #region Helpers
        
        private void BlacklistSkin(ulong skinId)
        {
            if (_config.BlackListedSkins.Contains(skinId))
            {
                return;
            }
            _config.BlackListedSkins = _config.BlackListedSkins.Concat(new ulong[] {skinId}).ToArray();
            SaveConfig();
        }
        private Item GetLookingAtItem(BasePlayer player)
        {
            var ray = new Ray(player.eyes.position, player.eyes.HeadForward());
            var entity = FindObject(ray, 10); // TODO: Make distance configurable

            if (entity != null && entity.GetItem() != null)
            {
                Puts(entity.GetItem().info.shortname);
                return entity.GetItem();
            }

            return null;
        }
        
        private static BaseEntity FindObject(Ray ray, float distance)
        {
            RaycastHit hit;
            return !Physics.Raycast(ray, out hit, distance) ? null : hit.GetEntity();
        }

        private double GetDiscount(BasePlayer player)
        {
            var discount = 0d;
            for (var x = 0; x < _config.VipDiscounts.Length; x++)
            {
                if (permission.UserHasPermission(player.UserIDString, vipPermissions + (x + 1).ToString()))
                {
                    discount = _config.VipDiscounts[x];
                }
            }
            return discount;
        }
        
        private DateTime GetServerTime()
        {
            return DateTime.Now ;
        }

        private string Color(string colorName, string opacity = "1.0")
        {
            if (!_config.Color.ContainsKey(colorName)) return "1.0 1.0 1.0 1.0";
            return _config.Color[colorName] + " " + opacity;
        }

        private int PlayerInventorySpaceAvailable(BasePlayer player)
        {
            var available = player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count;
            available += player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count;
            return available;
        }
        
        private WorkshopResult UnEscape(string json)
        {
            return JsonConvert.DeserializeObject<WorkshopResult>(Uri.UnescapeDataString(json));
        }

        private WorkShopItem UnEscapeItem(string json)
        {
            return JsonConvert.DeserializeObject<WorkShopItem>(Uri.UnescapeDataString(json));
        }
        
        static string CleanInput(string strIn)
        {
            // Replace invalid characters with empty strings.
            try {
                return Regex.Replace(strIn, @"[^\s\w\d\.@-]", "",
                    RegexOptions.None, TimeSpan.FromSeconds(1.5));
            }
            // If we timeout when replacing invalid characters,
            // we should return Empty.
            catch (RegexMatchTimeoutException) {
                return String.Empty;
            }
        }
        
        static string Hash(string input)
        {
            var hash = new SHA1Managed().ComputeHash(Encoding.UTF8.GetBytes(input));
            return string.Concat(hash.Select(b => b.ToString("x2")));
        }

        #endregion

        #region Discord

        private class DiscordMessage
        {
            public string content;
        }

        private void PostToDiscord(string message)
        {
            if (_config.DiscordChannelId == "" || _config.DiscordApiKey == "") return;
            
            message = JsonConvert.SerializeObject(new DiscordMessage { content = message }, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            webrequest.Enqueue($"https://discord.com/api/channels/{_config.DiscordChannelId}/messages",
                message,
                (i, s) =>
                { },
                null,
                RequestMethod.POST,
                new Dictionary<string, string> { ["Authorization"] = $"Bot {_config.DiscordApiKey}", ["Content-Type"] = "application/json" });
        }

        #endregion

        #region HumanNPC

        private void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            if (_config.HumanNpcIds.Contains(npc.userID))
            {
                ShowSkinShop(player.IPlayer, "", new string[0]);
            }
        }
        

        #endregion
    }
}
