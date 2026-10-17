import test from 'node:test'
import assert from 'node:assert/strict'
import { readFile, writeFile } from 'node:fs/promises'
import { PDFDocument, PDFSignature, PDFName } from 'pdf-lib'
import { generateIssuedPdf, issuedPdfDate, validateIssuedInput, emptyIssuedAsset } from '../app/utils/issuedForm.ts'
const template = await readFile(new URL('../app/assets/forms/it-equipment-issue-return-v2.pdf', import.meta.url))
const input = { user: 'QA User', employeeNo: '009876', assets: Array.from({length:6},(_,i)=>({brandModel:`Model ${i+1}`,serialNo:`SN${i+1}`,poNo:`PO${i+1}`,remark:`FA${i+1}`})) }
const when = new Date(2026,9,17,23,59)

test('date is local calendar and retains template format',()=>assert.equal(issuedPdfDate(when),'17-Oct-26'))
test('requires user, employee number and valid equipment rows',()=>{
 assert.ok(validateIssuedInput({...input,user:''}))
 assert.ok(validateIssuedInput({...input,assets:[emptyIssuedAsset()]}))
 assert.ok(validateIssuedInput({...input,assets:Array(7).fill(input.assets[0])}))
 assert.ok(validateIssuedInput({...input,assets:[{...emptyIssuedAsset(),serialNo:'orphan'}]}))
 assert.equal(validateIssuedInput(input),null)
})
test('fills all 24 asset cells plus B and Received By; preserves every other field and eight signatures',async()=>{
 const bytes=await generateIssuedPdf(template,input,when)
 const original=await PDFDocument.load(template);const out=await PDFDocument.load(bytes)
 const a=original.getForm();const b=out.getForm()
 assert.equal(out.getPageCount(),1)
 assert.equal(a.getFields().length,b.getFields().length)
 const changed=new Set([...Array.from({length:26},(_,i)=>`Text${i+1}`),'Text29','Date2'])
 for(const field of a.getFields()) {
   const name=field.getName(), other=b.getField(name)
   assert.equal(other.constructor.name,field.constructor.name)
   assert.equal(other.acroField.getWidgets().length,field.acroField.getWidgets().length)
   assert.deepEqual(other.acroField.getWidgets().map(w=>w.getRectangle()),field.acroField.getWidgets().map(w=>w.getRectangle()))
   if(!changed.has(name)) {
     assert.equal(other.acroField.dict.get(PDFName.of('V'))?.toString(),field.acroField.dict.get(PDFName.of('V'))?.toString(),name)
     assert.equal(other.acroField.getFlags(),field.acroField.getFlags(),name)
   }
 }
 for(let r=0;r<6;r++) for(const [c,key] of ['brandModel','serialNo','poNo','remark'].entries()) assert.equal(b.getTextField(`Text${r*4+c+1}`).getText(),input.assets[r][key])
 assert.equal(b.getTextField('Text25').getText(),input.user)
 assert.equal(b.getTextField('Text26').getText(),'009876')
 assert.equal(b.getTextField('Text29').getText(),input.user)
 assert.equal(b.getTextField('Date2').getText(),'17-Oct-26')
 assert.equal(b.getTextField('Date2').isReadOnly(),false)
 const sigs=b.getFields().filter(f=>f instanceof PDFSignature)
 assert.equal(sigs.length,8)
 for(const s of sigs) {assert.equal(s.isReadOnly(),false);assert.equal(s.acroField.dict.has(PDFName.of('V')),false)}
 // Reopen and edit the generated date, then save/reopen again.
 b.getTextField('Date2').setText('18-Oct-26')
 const edited=await PDFDocument.load(await out.save())
 assert.equal(edited.getForm().getTextField('Date2').getText(),'18-Oct-26')
 assert.equal(edited.getForm().getFields().filter(f=>f instanceof PDFSignature).length,8)
 if(process.env.ISSUED_QA_PDF) await writeFile(process.env.ISSUED_QA_PDF,bytes)
})
test('unused rows are blank and original template bytes are never changed',async()=>{
 const before=Buffer.from(template)
 const out=await PDFDocument.load(await generateIssuedPdf(template,{...input,assets:[input.assets[0]]},when))
 for(let i=5;i<=24;i++) assert.equal(out.getForm().getTextField(`Text${i}`).getText()??'','')
 assert.deepEqual(template,before)
})
test('rejects unsupported or unreadably long entries instead of clipping silently',async()=>{
 await assert.rejects(generateIssuedPdf(template,{...input,user:'名字'},when),/Latin/)
 await assert.rejects(generateIssuedPdf(template,{...input,user:'W'.repeat(500)},when),/too long/)
})
