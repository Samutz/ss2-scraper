namespace SS2Scraper;

public partial class Export
{
    public class ModMetadata
    {
        public string pluginFile = "";
        public int nexusId = 0;
        public string name = "";
        public string version = "";
    }

    public class Output
    {
        public string name = "";
        public bool isMaster;
        public bool isLight;
        public List<string> masters = [];
        public int totalItems = 0;
        public ModMetadata? metadata;
        public List<BuildingPlan> buildingPlans = [];
        public List<BuildingPlanSkin> buildingPlanSkins = [];
        public List<BaseItem> dynamicFlags = [];
        public List<Foundation> foundations = [];
        public List<PowerPole> powerPoles = [];
        public List<FurnitureStoreItem> furnitureStoreItems = [];
        public List<LeaderCard> leaderCards = [];
        public List<BaseItem> petStoreCreatures = [];
        public List<UnlockableCharacter> unlockableCharacters = [];
        public List<BeerRecipe> beerRecipes = [];
        public List<CityPlan> cityPlans = [];
        public List<WorldRepopulationCell> worldRepopCells = [];
        public List<HQRoom> hqRooms = [];
        public List<BaseItem> petNames = [];
        public List<UnitType> unitTypes = [];
    }

    public class BaseItem
    {
        public string formKey = "";
        public string editorId = "";
        public string name = "";
        public string description = "";
    }

    public class BuildingPlan : BaseItem
    {
        public UnlockableRequirements? requirements;
        public bool isPlayerSelectOnly = false;
        public string type = "";
        public string typeSubClass = "";
        public string size = "";
        public List<string> tags = [];
        public List<BuildingLevelPlan> levelPlans = [];
        public int maxLevel = 3;
        public int maxOccupants = 1;
        public bool isMarvel = false;
        public bool isBranching = false;
    }

    public class UnlockableRequirements
    {
        public bool hasRequirements = false;
        public List<string> plugins = [];
        public bool hasOtherRequirements = false;
    }

    public class BuildingLevelPlan : BaseItem
    {
        public int level = 1;
        public int maxOccupants = 1;
        public List<string> jobTitles = [];
        public List<string> jobUniform = [];
    }

    public class BuildingPlanSkin : BaseItem
    {
        public string targetPlan = "";
        public List<BaseItem> levelSkins = [];
        public bool isPlayerSelectOnly = false;
        public List<string> tags = [];
    }

    public class UniqueNpc : BaseItem
    {
        public ActorSpecial? special;
        public string plugin = "";
    }

    public class UnlockableCharacter : BaseItem
    {
        public UniqueNpc? targetActor;
    }

    public class LeaderCard : UnlockableCharacter
    {
        public BaseItem? majorTrait;
        public List<BaseItem> minorTraits = [];
        public List<BaseItem> weaknesses = [];
    }

    public class DynamicFlag : BaseItem
    {
        public UnlockableRequirements? requirements;
        public string FlagWaving = "";
        public string FlagDown = "";
        public string FlagWall = "";
        public string FlagHalfCircleFlag01 = "";
        public string FlagHalfCircleFlag02 = "";
        public string FlagBannerTownStatic = "";
        public string FlagBannerTownTorn = "";
        public string FlagBannerTownTornWaving = "";
    }

    public class CityPlan : BaseItem
    {
        public int maxLevel = 1;
        public bool isPlayerSelectOnly = false;
        public List<int> supportedNPCs = [0];
        public string targetSettlement = "";
        public List<string> plugins = [];
        public string author = "";
        public bool isOutpostPlan = false;
    }

    public class WorldRepopulationCell : BaseItem
    {
        public int maxPopulation = 0;
        public string author = "";
    }

    public class FurnitureStoreItem : BaseItem
    {
        public string workshopName = ""; // name as appears in workshop menu, might be different from shop inventory name
        public int vendorLevel = 1;
        public string type = "other";
        public int value = 0;
        public int displayType = 0;
    }

    public class PetStoreCreature : BaseItem
    {
        public int vendorLevel = 1;
        public int value = 0;
    }

    public class Foundation : BaseItem
    {
        public string workshopName = ""; // name as appears in workshop menu, might be different from foundation menu name
        public bool craftable = false;
        public bool terraformer = false;
        public int size = 0;
    }

    public class PowerPole : BaseItem
    {
        public string workshopName = "";
        public bool craftable = false;
        public int height = 0;
        public bool hasLight = false;
        public int plotSize = 0;
    }

    public class HQRoom : BaseItem
    {
        public string targetUpgradeSlot = "";
        public List<string> availableSlots = [];
        public List<string> functions = [];
        public string type = "";
        public string author = "";
        public string shape = "";
        public string primaryDepartment = "";
    }

    public class HQRoomLayout : BaseItem
    {
        public string author = "";
    }

    public class BeerRecipe : BaseItem
    {
        public string recipeName = "";
        public string mash = "";
        public List<string> flavorings = [];
    }

    public class UnitType : BaseItem
    {
        public int strengthRating = 1;
        public List<UnitRank> ranks = [];
        public UnlockableRequirements? requirements;
        public string shortDescription = "";
        public string defaultOutfit = "";

    }

    public class UnitRank : BaseItem
    {
        public int rank = 0;
        public string rankName = "";
    }

    public class BoundsSize
    {
        public int X = 0;
        public int Y = 0;
        public int Z = 0;
    }

    public class ActorSpecial
    {
        public int Strength = 0;
        public int Perception = 0;
        public int Endurance = 0;
        public int Charisma = 0;
        public int Intelligence = 0;
        public int Agility = 0;
        public int Luck = 0;
    }
}