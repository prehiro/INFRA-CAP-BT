using Api.Contracts;
using Api.Data;
using Api.Domain;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Row = Api.Domain.Record;

// Real CRUD/validation against a disposable relational DB. Never reads appsettings or touches DEV/PROD.
var passed = 0;
void Assert(bool value, string message) { if (!value) throw new Exception(message); }
async Task Reject(Func<Task> action, string field) {
    try { await action(); } catch (RecordValidationException ex) {
        Assert(ex.Errors.ContainsKey(field), $"Expected {field}, got {string.Join(',', ex.Errors.Keys)}");return;
    }
    throw new Exception($"Expected duplicate rejection for {field}");
}
async Task Case(string slug, string[] identities, string name, Func<AppDbContext, DynamicRecordService, DynamicEntity, Row, Row, Task> body) {
    await using var conn = new SqliteConnection("Data Source=:memory:");await conn.OpenAsync();
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
    await db.Database.EnsureCreatedAsync();
    var entity = new DynamicEntity { Slug=slug, Name=slug, DisplayField="owner" };
    entity.Fields.Add(new DynamicField { Name="nomor", Label="No", IsRequired=true, IsUnique=true });
    entity.Fields.Add(new DynamicField { Name="owner", Label="Owner" });
    foreach (var key in identities) entity.Fields.Add(new DynamicField { Name=key, Label=key, IsUnique=false });
    db.Entities.Add(entity);await db.SaveChangesAsync();
    Row Seed(string no, string suffix) {
        var row = new Row { Entity=entity, CreatedBy="isolated-test" };
        foreach (var field in entity.Fields) row.Values.Add(new RecordValue { Field=field, TextValue=field.Name switch {
            "nomor"=>no, "owner"=>"Original owner", _=>suffix=="A" ? $"  {field.Name}-legacy  " : $"{field.Name}-other"
        }});
        db.Records.Add(row);return row;
    }
    var original=Seed("1","A");var other=Seed("2","B");await db.SaveChangesAsync();
    // Deliberate LEGACY duplicates are inserted directly ONLY inside the isolated in-memory database.
    foreach (var key in identities) {
        var field=entity.Fields.Single(f=>f.Name==key);
        var legacy=Seed("3-"+key,"B");
        legacy.Values.Single(v=>v.Field.Name==key).TextValue=$"{key}-LEGACY";
    }
    await db.SaveChangesAsync();
    await body(db,new DynamicRecordService(db),entity,original,other);
    Console.WriteLine($"PASS {slug}: {name}");passed++;
}
Dictionary<string,object?> Input(Row row) => row.Values.Where(v=>v.Field.Name!="nomor").ToDictionary(v=>v.Field.Name,v=>(object?)v.TextValue);
foreach (var (slug, keys) in new[] { ("pc_ledger", new[]{"japan_hostname","computer_sn"}), ("factory_pc", new[]{"sn"}) }) {
    await Case(slug, keys, "owner-only edit retains legacy duplicate identities and No", async (db,svc,e,a,b)=> {
        var before=a.Values.ToDictionary(v=>v.Field.Name,v=>v.TextValue);var input=Input(a);input["owner"]="New owner";
        await svc.UpdateAsync(e.Id,a.Id,new(input),"isolated-test");db.ChangeTracker.Clear();
        var saved=await svc.GetAsync(e.Id,a.Id);
        Assert((string?)saved.Values["owner"]=="New owner","Owner did not persist");
        foreach(var key in keys.Append("nomor")) Assert((string?)saved.Values[key]==before[key],$"Unchanged {key} was overwritten");
    });
    await Case(slug, keys, "case/whitespace-equivalent values retain legacy identity", async (db,svc,e,a,b)=> {
        var input=Input(a);foreach(var key in keys) input[key]=input[key]!.ToString()!.Trim().ToLowerInvariant();
        await svc.UpdateAsync(e.Id,a.Id,new(input),"isolated-test");
    });
    foreach (var key in keys) {
        await Case(slug,keys,$"changed {key} rejects normalized duplicate on another active record",async(db,svc,e,a,b)=> {
            var input=Input(a);input[key]="  "+b.Values.Single(v=>v.Field.Name==key).TextValue!.ToUpperInvariant()+"  ";
            await Reject(()=>svc.UpdateAsync(e.Id,a.Id,new(input),"isolated-test"),key);
            Assert(a.Values.Single(v=>v.Field.Name==key).TextValue!.Contains("legacy"),"Rejected update mutated source");
        });
        await Case(slug,keys,$"new record rejects {key} duplicates even when metadata IsUnique is false",async(db,svc,e,a,b)=> {
            var input=Input(a);input["nomor"]="99";foreach(var k in keys) input[k]="FRESH-"+k;
            input[key]=" "+a.Values.Single(v=>v.Field.Name==key).TextValue!.Trim().ToUpperInvariant()+" ";
            await Reject(()=>svc.CreateAsync(e.Id,new(input),"isolated-test"),key);
        });
        await Case(slug,keys,$"changed {key} accepts a new unique value while other legacy identity stays",async(db,svc,e,a,b)=> {
            var input=Input(a);input[key]="NEW-UNIQUE-"+key;
            await svc.UpdateAsync(e.Id,a.Id,new(input),"isolated-test");
            Assert(a.Values.Single(v=>v.Field.Name==key).TextValue=="NEW-UNIQUE-"+key,"Unique value did not persist");
        });
        await Case(slug,keys,$"deleted record does not reserve {key}",async(db,svc,e,a,b)=> {
            b.Values.Single(v=>v.Field.Name==key).TextValue="ONLY-DELETED-"+key;
            b.IsDeleted=true;await db.SaveChangesAsync();var input=Input(a);input[key]=b.Values.Single(v=>v.Field.Name==key).TextValue;
            await svc.UpdateAsync(e.Id,a.Id,new(input),"isolated-test");
        });
    }
    await Case(slug,keys,"new unique record is accepted",async(db,svc,e,a,b)=> {
        var input=Input(a);input["nomor"]="100";foreach(var key in keys) input[key]="UNIQUE-"+key;
        var saved=await svc.CreateAsync(e.Id,new(input),"isolated-test");Assert(saved.Id!=a.Id,"New ID not generated");
    });
    await Case(slug,keys,"unrelated unique No remains enforced",async(db,svc,e,a,b)=> {
        var input=Input(a);input["nomor"]="2";await Reject(()=>svc.UpdateAsync(e.Id,a.Id,new(input),"isolated-test"),"nomor");
    });
}
await Case("unrelated_entity",new[]{"code"},"other entities do not inherit legacy identity exception",async(db,svc,e,a,b)=> {
    var field=e.Fields.Single(f=>f.Name=="code");field.IsUnique=true;
    b.Values.Single(v=>v.Field.Name=="code").TextValue=a.Values.Single(v=>v.Field.Name=="code").TextValue;
    await db.SaveChangesAsync();
    await Reject(()=>svc.UpdateAsync(e.Id,a.Id,new(Input(a)),"isolated-test"),"code");
});

// Both real register slugs share one serial namespace. Standby and Dispose are status
// views of PC Ledger; a soft-deleted source must not reserve its S/N after conversion.
await using (var conn = new SqliteConnection("Data Source=:memory:")) {
    await conn.OpenAsync();
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
    await db.Database.EnsureCreatedAsync();
    static DynamicField Field(string name) => new() { Name=name, Label=name };
    var ledger = new DynamicEntity { Slug="pc_ledger", Name="PC Ledger", Fields=new List<DynamicField>
        { Field("nomor"), Field("computer_sn"), Field("asset_status") } };
    var factory = new DynamicEntity { Slug="factory_pc", Name="Factory PC", Fields=new List<DynamicField>
        { Field("nomor"), Field("sn"), Field("status") } };
    db.Entities.AddRange(ledger,factory);await db.SaveChangesAsync();
    var svc = new DynamicRecordService(db);
    var standby = await svc.CreateAsync(ledger.Id,new(new Dictionary<string,object?>
        { ["nomor"]="1", ["computer_sn"]="STANDBY-01", ["asset_status"]="Standby" }),"isolated-test");
    var activeFactory = await svc.CreateAsync(factory.Id,new(new Dictionary<string,object?>
        { ["nomor"]="1", ["sn"]="FACTORY-01", ["status"]="Active" }),"isolated-test");
    await Reject(()=>svc.CreateAsync(ledger.Id,new(new Dictionary<string,object?>
        { ["nomor"]="2", ["computer_sn"]=" standby-01 " }),"isolated-test"),"computer_sn");
    await Reject(()=>svc.CreateAsync(factory.Id,new(new Dictionary<string,object?>
        { ["nomor"]="2", ["sn"]="STANDBY-01" }),"isolated-test"),"sn");
    await Reject(()=>svc.CreateAsync(ledger.Id,new(new Dictionary<string,object?>
        { ["nomor"]="2", ["computer_sn"]="factory-01" }),"isolated-test"),"computer_sn");
    await svc.UpdateAsync(ledger.Id,standby.Id,new(new Dictionary<string,object?>
        { ["asset_status"]="Plan Dispose" }),"isolated-test");
    try { await svc.CreateAsync(ledger.Id,new(new Dictionary<string,object?>
            { ["nomor"]="2", ["computer_sn"]="STANDBY-01" }),"isolated-test");
          throw new Exception("Dispose duplicate was accepted"); }
    catch(RecordValidationException ex) { Assert(ex.Errors["computer_sn"].Contains("Dispose (Status: Plan Dispose)"),
        "Dispose location/status missing from server error"); }
    await Reject(()=>svc.UpdateAsync(ledger.Id,standby.Id,new(new Dictionary<string,object?>
        { ["computer_sn"]="FACTORY-01" }),"isolated-test"),"computer_sn");
    await svc.UpdateAsync(ledger.Id,standby.Id,new(new Dictionary<string,object?>
        { ["computer_sn"]=" standby-01 " }),"isolated-test");
    await svc.DeleteAsync(ledger.Id,standby.Id,"isolated-test");
    await svc.CreateAsync(ledger.Id,new(new Dictionary<string,object?>
        { ["nomor"]="2", ["computer_sn"]="STANDBY-01" }),"isolated-test");
    await svc.DeleteAsync(factory.Id,activeFactory.Id,"isolated-test");
    await svc.CreateAsync(ledger.Id,new(new Dictionary<string,object?>
        { ["nomor"]="3", ["computer_sn"]="FACTORY-01" }),"isolated-test");
    Console.WriteLine("PASS cross-register serial collisions, status, edits and archive");passed++;
}

// Reverse transfers run inside a disposable relational database as a permanent regression.
await using (var conn = new SqliteConnection("Data Source=:memory:")) {
    await conn.OpenAsync();
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
    await db.Database.EnsureCreatedAsync();
    static DynamicField Field(string name,int max=200) => new() {
        Name=name, Label=name, MaxLength=max, IsRequired=name=="nomor", IsUnique=name=="nomor"
    };
    var ledger = new DynamicEntity { Slug="pc_ledger", Name="PC Ledger", Fields=new List<DynamicField>
        { Field("nomor",30),Field("staff_name"),Field("email"),Field("chassis",50),
          Field("computer_model",100),Field("computer_sn",100),Field("os_name",100),
          Field("remark2"),Field("remark3"),Field("asset_status",32) } };
    var factory = new DynamicEntity { Slug="factory_pc", Name="Factory PC", Fields=new List<DynamicField>
        { Field("nomor",30),Field("pic"),Field("email"),Field("chassis",50),
          Field("model",100),Field("sn",100),Field("os",100),Field("remarks",500),Field("status",50) } };
    db.Entities.AddRange(ledger,factory);await db.SaveChangesAsync();
    var svc = new DynamicRecordService(db,new LogbookNumberService(db));
    var disposition = new DeviceDispositionService(db,svc,new AuditService(db));
    foreach (var (action,expected) in new[] { ("Domain PC",""),("Standby","Standby"),("Plan Dispose","Plan Dispose") }) {
        var serial="REVERSE-"+action;
        var source=await svc.CreateAsync(factory.Id,new(new Dictionary<string,object?> {
            ["pic"]="Original",["email"]="owner@example.invalid",["chassis"]="Desktop",
            ["model"]="Model A",["sn"]=serial,["os"]="Test OS",["remarks"]="Original remark",["status"]="Active"
        }),"isolated-test");
        await Reject(()=>svc.UpdateAsync(factory.Id,source.Id,new(new Dictionary<string,object?>
            { ["status"]=action }),"isolated-test"),"status");
        var target=await disposition.ConvertToLedgerAsync(factory.Id,source.Id,new(new Dictionary<string,object?>
            { ["status"]=action,["pic"]="Edited owner" }),"isolated-test");
        Assert(target.Values["staff_name"]?.ToString()=="Edited owner","Reverse transfer lost edited PIC");
        Assert(target.Values["computer_sn"]?.ToString()==serial,"Reverse transfer lost serial");
        Assert(target.Values["email"]?.ToString()=="owner@example.invalid","Reverse transfer lost email");
        Assert(target.Values["computer_model"]?.ToString()=="Model A","Reverse transfer lost model");
        Assert(target.Values["remark2"]?.ToString()=="Original remark","Reverse transfer lost remarks");
        Assert((target.Values["asset_status"]?.ToString()??"")==expected,"Wrong target page status");
        Assert((await db.Records.SingleAsync(r=>r.Id==source.Id)).IsDeleted,"Factory source not archived");
        Assert(!string.IsNullOrWhiteSpace(target.Values["nomor"]?.ToString()),"Ledger number missing");
        passed++;
    }
    var tooLong=await svc.CreateAsync(factory.Id,new(new Dictionary<string,object?>
        { ["pic"]="Keep me",["sn"]="REVERSE-LONG",["remarks"]=new string('A',450),["status"]="Active" }),"isolated-test");
    await Reject(()=>disposition.ConvertToLedgerAsync(factory.Id,tooLong.Id,new(new Dictionary<string,object?>
        { ["status"]="Domain PC",["pic"]="Must rollback" }),"isolated-test"),"remarks");
    db.ChangeTracker.Clear();
    Assert((await svc.GetAsync(factory.Id,tooLong.Id)).Values["pic"]?.ToString()=="Keep me",
        "Failed transfer changed Factory data");
    Assert(await db.AuditEntries.CountAsync(a=>a.Action==AuditActions.RecordTransfer)==3,
        "Failed transfer created an audit entry");
    Console.WriteLine("PASS reverse Domain/Standby/Dispose transfer, audit and rollback");passed++;
}
Console.WriteLine($"Passed {passed} isolated relational CRUD regressions; no DEV/PROD database accessed.");
