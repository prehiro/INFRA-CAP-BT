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
Console.WriteLine($"Passed {passed} isolated relational CRUD regressions; no DEV/PROD database accessed.");
