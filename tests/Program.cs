using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Navy1851;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

int assertions = 0;
void Check(bool result, string what) { assertions++; if (!result) throw new Exception(what); }
Check(Chamber.Clamp(-10)==0 && Chamber.Clamp(99)==6, "Invalid save state is clamped");
Check(!Chamber.CanFire(0,900,1000,0), "An empty cylinder cannot fire");
Check(!Chamber.CanFire(1,349,1000,0), "A short click cannot fire");
Check(Chamber.CanFire(1,350,1000,0), "A cocked round can fire");
Check(!Chamber.CanFire(6,900,1000,1001), "Cooldown cannot be skipped");
Check(Chamber.CanFire(6,900,1001,1001), "Cooldown boundary permits next shot");
Check(!Chamber.CanLoad(6,10000) && !Chamber.CanLoad(0,1399), "Full and partial reloads are rejected");
int ammo=6,rounds=0;
for(int i=0;i<6;i++) if(Chamber.CanLoad(rounds,1400)){ammo--;rounds++;}
Check(rounds==6 && ammo==0, "Six completed loads consume six charges");
for(int i=0;i<6;i++){Check(Chamber.CanFire(rounds,900,10000+i*700,10000+i*700),"Loaded shot");rounds--;}
Check(!Chamber.CanFire(rounds,900,20000,0),"Seventh shot is blocked");
// Exercise the same held-trigger state machine used by the held-control tick.
var heldTrigger = new FiringCycle(1000);
int cylinder = 6, fired = 0;
var shotTimes = new List<long>();
for (long time = 1000; time <= 7000; time += 10)
{
    if (heldTrigger.TryFire(cylinder, time, 0, out int left))
    { cylinder = left; fired++; shotTimes.Add(time); }
}
Check(fired == 6 && cylinder == 0 && !heldTrigger.Active, "One continuous hold fires exactly six rounds and stops");
Check(shotTimes.SequenceEqual(new long[]{1350,2050,2750,3450,4150,4850}), "Cocking and repeat cadence are preserved");
Check(!heldTrigger.TryFire(6, 9000, 0, out _), "Refilling an exhausted held cycle cannot restart firing");
var released = new FiringCycle(0);
Check(released.TryFire(6,350,0,out _), "Initial shot on hold");
released.Stop();
Check(!released.TryFire(5,2000,0,out _), "Release cancels all future shots without a release shot");
var interrupted = new FiringCycle(0); interrupted.Stop();
Check(!interrupted.TryFire(6,1000,0,out _), "Interruption before cocking consumes nothing");
var lagged = new FiringCycle(0);
Check(lagged.TryFire(6,5000,0,out _) && !lagged.TryFire(5,5000,0,out _) && !lagged.TryFire(5,5699,0,out _), "Delayed ticks cannot catch up with a burst");
Check(lagged.TryFire(5,5700,0,out _), "Repeating resumes one full interval after delayed shot");
var switched = new FiringCycle(0);
Check(!switched.TryFire(6,350,700,out _) && switched.TryFire(6,700,700,out _), "A new hold respects actor-wide cooldown");
var empty = new FiringCycle(0);
Check(!empty.TryFire(0,1000,0,out _) && !empty.Active, "Starting empty terminates the firing cycle");
var aimControls = new WeaponControls();
aimControls.Update(false, true, false, 100);
Check(aimControls.Trigger == null, "Right mouse alone never fires");
Check(aimControls.Spread(100) == WeaponControls.HipSpread, "Aiming must settle before accuracy improves");
Check(Math.Abs(aimControls.Spread(500)-WeaponControls.SightedSpread)<0.00001f, "Settled sights provide tighter spread");
aimControls.Update(true,true,false,500);
Check(aimControls.Aiming && aimControls.Trigger != null, "Both mouse buttons aim and fire simultaneously");
aimControls.Update(true,false,false,1000);
Check(aimControls.Spread(1000)==WeaponControls.HipSpread && aimControls.Trigger != null, "Releasing aim restores hip spread while trigger remains held");
aimControls.Update(true,true,true,1200);
Check(aimControls.Reloading && !aimControls.Aiming && aimControls.Trigger==null, "Reload cancels firing and aiming");
Check(!aimControls.CanLoad(0,2599) && aimControls.CanLoad(0,2600), "Reload still requires a complete interval");
aimControls.Update(false,false,false,2700);
Check(!aimControls.Reloading && aimControls.Trigger==null, "Released inputs stop reload and fire");
var attributes=new TreeAttribute();attributes.SetInt("navy1851:rounds",4);
using(var stream=new MemoryStream()){
 using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true)) attributes.ToBytes(writer);
 stream.Position=0;var restored=new TreeAttribute();using(var reader=new BinaryReader(stream))restored.FromBytes(reader);
 Check(restored.GetInt("navy1851:rounds")==4,"Rounds survive the actual API attribute binary roundtrip");
}
string root=Path.GetFullPath(args[0]);
string targetVersion=JObject.Parse(File.ReadAllText(Path.Combine(root,"modinfo.json")))["dependencies"]!["game"]!.ToString();
Check(typeof(Item).Assembly.GetName().Version!.ToString(3) == targetVersion, "Checks run against the exact game version declared in modinfo");
var log=DispatchProxy.Create<ILogger, Stub>();
((Stub)(object)log).Handler=(m,a)=>{if(m.Name is "Error" or "Fatal" or "Warning")throw new Exception(m.Name+": "+string.Join(" ",a??[]));return null;};
var allItems=new Dictionary<string,Item>();
foreach(string code in new[]{"game:ingot-iron","game:ingot-brass","game:plank-walnut","game:ingot-copper","game:flaxfibers","game:blastingpowder","navy1851:revolver","navy1851:papercharge"})
 allItems[code]=new Item{Code=new AssetLocation(code)};
var recipeIndex = new System.Collections.Generic.OrderedDictionary<IRecipeIngredientBase,List<IRecipeBase>>();
var world=DispatchProxy.Create<IWorldAccessor, Stub>();
((Stub)(object)world).Handler=(m,a)=>m.Name switch {
 "GetItem" => allItems.GetValueOrDefault(a![0]!.ToString()!),
 "get_Logger" => log,
 "get_FastSearchRecipesByIngredient" => recipeIndex,
 "get_Side" => EnumAppSide.Client,
 _ => m.ReturnType.IsValueType ? Activator.CreateInstance(m.ReturnType) : null
};
foreach(string path in Directory.GetFiles(Path.Combine(root,"assets/navy1851/shapes/item"),"*.json")){
 var shape=JsonConvert.DeserializeObject<Shape>(File.ReadAllText(path))!;
 shape.ResolveReferences(log,Path.GetFileName(path));
 Check(shape.Elements.Length>0,"Native Shape parser reads elements");
 var names=new HashSet<string>();
 foreach(var el in shape.Elements){
  Check(names.Add(el.Name!),"Unique element name");
  Check(el.From!.Zip(el.To!).All(p=>p.First<p.Second),"Positive cuboid extent");
  Check(el.FacesResolved is {Length:6} && el.FacesResolved.All(f=>f!=null),"All faces resolve through native API");
  foreach(var face in el.FacesResolved!){
   Check(shape.Textures.ContainsKey(face.Texture),"Native face texture resolves");
   var sz=shape.TextureSizes[face.Texture];var uv=face.Uv;
   Check(uv[0]>=0&&uv[1]>=0&&uv[2]<=sz[0]&&uv[3]<=sz[1],"UV bounds");
  }
 }
 Console.WriteLine($"Native shape parse: {Path.GetFileName(path)}, {shape.Elements.Length} elements");
}
foreach(string path in Directory.GetFiles(Path.Combine(root,"assets/navy1851/recipes/grid"),"*.json")){
 var recipe=JsonConvert.DeserializeObject<GridRecipe>(File.ReadAllText(path))!;
 Check(recipe.Resolve(world,Path.GetFileName(path)),"Native grid recipe resolves");
 Check(recipe.ResolvedIngredients!.Length==recipe.Width*recipe.Height,"Grid dimensions resolve");
 Console.WriteLine("Native recipe resolve: "+Path.GetFileName(path));
}
foreach(string path in Directory.GetFiles(Path.Combine(root,"assets/navy1851/itemtypes"),"*.json")){
 var item=JObject.Parse(File.ReadAllText(path));
 Check(item["shape"]?["base"]!=null,"Item shape reference exists");
 string asset=item["shape"]!["base"]!.ToString().Split(':')[1];
 Check(File.Exists(Path.Combine(root,"assets/navy1851/shapes/"+asset+".json")),"Item points to shipped shape");
 foreach(var t in ((JObject)item["textures"]!).Properties()){
  string tex=t.Value["base"]!.ToString().Split(':')[1];
  Check(File.Exists(Path.Combine(root,"assets/navy1851/textures/"+tex+".png")),"Item points to shipped PNG");
 }
}
Console.WriteLine($"PASS: {assertions} assertions. API assembly: {typeof(Item).Assembly.GetName().Version}");
Console.WriteLine("These offline checks do not establish in-game input, raycast, sound, or hand-transform acceptance.");
public class Stub:DispatchProxy{
 public System.Func<MethodInfo,object?[]?,object?> Handler = (_,_)=>null;
 protected override object? Invoke(MethodInfo? targetMethod,object?[]? args)=>Handler(targetMethod!,args);
}
