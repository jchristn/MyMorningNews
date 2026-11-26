#!/usr/bin/env python3
"""Maintain the platform resource bindings across C#, JavaScript, and Python. (c) 2026 Lattice AI LLC."""
import importlib.util
import re
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('models', ROOT / 'sdk/csharp/tools/generate_models.py')
gen = importlib.util.module_from_spec(spec); spec.loader.exec_module(gen)
alltypes, _ = gen.scan(str(ROOT / 'src/MyMorningNews.Core'))
# name, HTTP method, path, response, request body type, paging options, documentation
GROUPS = {
'Catalog': [
('list','GET','/v1.0/api/catalog','CatalogEntryResponse',None,'CatalogListOptions','Browse published system briefings and account eligibility. subscribed true returns only active subscriptions, false only the rest; sortBy accepts title.'),
('subscriptions','GET','/v1.0/api/catalog/subscriptions','CatalogSubscription',None,'ListOptions','List private subscriptions, including suspended subscriptions.'),
('subscribe','PUT','/v1.0/api/catalog/{id}/subscription','CatalogSubscription','CatalogSubscriptionRequest',None,'Subscribe or update delivery preferences. Separate system subscription limits apply.'),
('unsubscribe','DELETE','/v1.0/api/catalog/{id}/subscription','void',None,None,'Unsubscribe without deleting canonical content.'),
('issues','GET','/v1.0/api/catalog/{id}/issues','CatalogIssueResponse',None,'ListOptions','List issues readable through this subscription.'),
('allIssues','GET','/v1.0/api/catalog/issues','CatalogIssueResponse',None,'CatalogIssueListOptions','List shared issues across all current subscriptions.'),
('issue','GET','/v1.0/api/catalog/{id}/issues/{issueId}','CatalogIssueResponse',None,None,'Read shared content without exposing canonical workspace internals.'),
('audio','GET','/v1.0/api/catalog/{id}/issues/{issueId}/audio','DownloadResult',None,None,'Download authenticated shared audio. Playback does not consume generation allowance.'),
('progress','GET','/v1.0/api/catalog/{id}/issues/{issueId}/progress','BriefingProgress',None,None,'Read private listening progress.'),
('saveProgress','PUT','/v1.0/api/catalog/{id}/issues/{issueId}/progress','BriefingProgress','BriefingProgressRequest',None,'Save private listening progress for a shared issue.')],
'Billing': [
('status','GET','/v1.0/api/account/billing','BillingStatusResponse',None,None,'Read offers, native account binding, verified subscriptions and effective access.'),
('checkout','POST','/v1.0/api/account/billing/checkout','BillingRedirectResponse','BillingCheckoutRequest',None,'Create or resume one account-bound Stripe checkout. A redirect never grants access.'),
('portal','POST','/v1.0/api/account/billing/portal','BillingRedirectResponse',None,None,'Open management for the verified Stripe customer.'),
('verifyPurchase','POST','/v1.0/api/account/billing/purchases','EffectivePlan','BillingPurchaseRequest',None,'Verify or restore an account-bound Apple or Google purchase.')],
'Providers': [
('list','GET','/v1.0/api/account/providers','ProviderConnectionsResponse',None,None,'List only this account\'s masked provider connections.'),
('save','PUT','/v1.0/api/account/providers','ProviderConnection','ProviderConnectionRequest',None,'Connect or rotate a write-only model or speech API key. Clears validation.'),
('test','POST','/v1.0/api/account/providers/{id}/test','JsonObject',None,None,'Run a fixed small probe with this account\'s credential. Six probes per minute.'),
('remove','DELETE','/v1.0/api/account/providers/{id}','JsonObject',None,None,'Remove a private key. BYOK calls fail closed, without platform fallback.'),
('setFunding','PUT','/v1.0/api/account/funding','EffectivePlan','FundingModeRequest',None,'Choose Personal or Platform funding. A verified chat connection is required for BYOK.')],
'AdminCatalog': [
('list','GET','/v1.0/api/admin/catalog','CatalogPublication',None,'ListOptions','System administrator: list every publication lifecycle state.'),
('get','GET','/v1.0/api/admin/catalog/{id}','CatalogPublication',None,None,'System administrator: read a canonical publication.'),
('create','POST','/v1.0/api/admin/catalog','CatalogPublication','CatalogPublicationRequest',None,'System administrator: create a canonical system topic and publication.'),
('update','PUT','/v1.0/api/admin/catalog/{id}','CatalogPublication','CatalogPublicationRequest',None,'System administrator: update publication with current revision for conflict protection.'),
('issues','GET','/v1.0/api/admin/catalog/{id}/issues','CatalogIssueResponse',None,'ListOptions','System administrator: inspect canonical issue projections.'),
('research','GET','/v1.0/api/admin/catalog/{id}/research','CatalogResearchResponse',None,None,'System administrator: read canonical research and schedule configuration.'),
('saveResearch','PUT','/v1.0/api/admin/catalog/{id}/research','CatalogResearchResponse','CatalogResearchRequest',None,'System administrator: configure canonical research and schedule.'),
('run','POST','/v1.0/api/admin/catalog/{id}/run','Run',None,None,'System administrator: start a canonical research run.')],
'AdminEntitlements': [
('get','GET','/v1.0/tenants/{tenantId}/users/{userId}/entitlements','EffectivePlan',None,None,'System administrator: inspect effective user allowances and provenance.'),
('set','PUT','/v1.0/tenants/{tenantId}/users/{userId}/entitlements','EffectivePlan','EntitlementOverrideRequest',None,'System administrator: override one limit with an audit reason and optional expiry.'),
('clear','DELETE','/v1.0/tenants/{tenantId}/users/{userId}/entitlements/{featureKey}','void',None,None,'System administrator: remove an override and restore plan inheritance.')]
}
def snake(s): return re.sub(r'([a-z0-9])([A-Z])',r'\1_\2',s).lower()
def camel(s): return s[0].lower()+s[1:]
def path_expr(path,lang):
 parts=re.split(r'(\{\w+\})',path); out=[]
 for p in parts:
  if not p: continue
  if p.startswith('{'):
   name=p[1:-1]
   out.append('MyMorningNewsClient.Segment('+name+', nameof('+name+'))' if lang=='cs' else 'seg('+name+', "'+name+'")' if lang=='js' else '_seg('+snake(name)+')')
  else: out.append('"'+p+'"')
 return ' + '.join(out)
def replace_block(path, text, marker, anchor):
 s=path.read_text(); start=marker+' BEGIN PLATFORM'; end=marker+' END PLATFORM'
 if start in s: s=s[:s.index(start)]+s[s.index(end)+len(end):].lstrip('\n')
 s=s.replace(anchor,start+'\n'+text+'\n'+end+'\n\n'+anchor,1);path.write_text(s)
js=[];ts=[];py=[]
for group, methods in GROUPS.items():
 name=group+'Resource';js.append('/** Platform account/catalog API. All authorization is enforced by the server. */\nexport class '+name+' extends ResourceGroup {');ts.append('export declare class '+name+' extends ResourceGroup {');py.append('class '+name+'(_Resource):\n    """Platform account/catalog API with server-enforced ownership and permissions."""')
 cs='namespace MyMorningNews.Sdk.Resources\n{\n    using System;\n    using System.Net.Http;\n    using System.Text.Json.Nodes;\n    using System.Threading;\n    using System.Threading.Tasks;\n    using MyMorningNews.Sdk.Models;\n    using MyMorningNews.Sdk.Options;\n    using MyMorningNews.Sdk.Requests;\n\n    /// <summary>Platform account/catalog API. Stateless and safe to share; server authorization is authoritative.</summary>\n    public class '+name+' : ResourceBase\n    {\n        /// <summary>Bind the resource to a client.</summary>\n        public '+name+'(MyMorningNewsClient client) : base(client) { }\n'
 for method,verb,path,result,body,paging,doc in methods:
  ids=re.findall(r'\{(\w+)\}',path);cp=['string '+i for i in ids];jp=ids.copy();tp=[i+': string' for i in ids];pp=[snake(i)+': str' for i in ids]
  if body: cp.append(body+' body');jp.append('body');tp.append('body: '+body);pp.append('body: Mapping[str, Any]')
  if paging: cp.append(paging+'? options = null');jp.append('options = {}');tp.append('options?: '+('ListOptions & { category?: string; language?: string; subscribed?: boolean }' if paging=='CatalogListOptions' else 'ListOptions & { audioOnly?: boolean }' if paging=='CatalogIssueListOptions' else 'ListOptions'));pp.append('**filters: Any')
  if result=='DownloadResult': cp.append('ByteRange? range = null');jp.append('options = {}');tp.append('options?: DownloadOptions');pp.append('byte_range: ByteRange = None')
  cp.append('CancellationToken token = default')
  cr='EnumerationResult<'+result+'>' if paging else result;tr='EnumerationResult<'+result+'>' if paging else {'void':'null','JsonObject':'Record<string, unknown>'}.get(result,result)
  cmethod=method[0].upper()+method[1:]+'Async';cs+='\n        /// <summary>'+doc+'</summary>\n        /// <exception cref="MyMorningNewsException">The server rejected the operation.</exception>\n        public Task'+('' if result=='void' else '<'+cr+'>')+' '+cmethod+'('+', '.join(cp)+')\n        {\n'
  if body:cs+='            ArgumentNullException.ThrowIfNull(body);\n'
  query='(options ?? new '+paging+'()).ToQuery()' if paging else 'null';b='body' if body else 'null'
  if result=='DownloadResult':call='Client.DownloadAsync(HttpMethod.Get, '+path_expr(path,'cs')+', null, null, range, null, token)'
  else:call='Client.SendAsync'+('' if result=='void' else '<'+cr+'>')+'(HttpMethod.'+verb.title()+', '+path_expr(path,'cs')+', '+query+', '+b+', token)'
  cs+='            return '+call+';\n        }\n'
  jopts=[]
  if body:jopts.append('body: requireValue(body, "body")')
  if paging:jopts.append('query: options')
  if result=='DownloadResult':jopts+=['raw: true','signal: options.signal','range: options.range']
  js.append('    /** '+doc+' */\n    '+method+'('+', '.join(jp)+') { return this._req("'+verb+'", '+path_expr(path,'js')+(', { '+', '.join(jopts)+' }' if jopts else '')+'); }')
  ts.append('    /** '+doc+' */\n    '+method+'('+', '.join(tp)+'): Promise<'+tr+'>;')
  presult={'void':'None','JsonObject':'Dict[str, Any]','DownloadResult':'Download'}.get(result,result);presult='Page['+presult+']' if paging else presult
  py.append('\n    def '+snake(method)+'(self'+(', '+', '.join(pp) if pp else '')+') -> '+presult+':\n        """'+doc+'"""')
  if paging:pcall='self._page('+result+', '+path_expr(path,'py')+', filters)'
  elif result=='DownloadResult':pcall='self._download('+path_expr(path,'py')+', byte_range=byte_range)'
  else:pcall='self._'+('json(' if result in ['void','JsonObject'] else 'one('+result+', ')+'"'+verb+'", '+path_expr(path,'py')+(', body=body' if body else '')+')'
  py.append('        '+('return ' if result!='void' else '')+pcall)
 cs+='    }\n}\n';(ROOT/'sdk/csharp/MyMorningNews.Sdk/Resources'/ (name+'.cs')).write_text(cs)
 js.append('}\n');ts.append('}\n');py.append('\n')
replace_block(ROOT/'sdk/js/mymorningnews-sdk.js','\n'.join(js),'//','export class AccountResource')
replace_block(ROOT/'sdk/js/index.d.ts','\n'.join(ts),'//','export declare class AccountResource')
replace_block(ROOT/'sdk/python/mymorningnews_sdk.py','\n'.join(py),'#','class AccountResource')
for group in GROUPS:
 name=group+'Resource';prop=camel(group)
 p=ROOT/'sdk/js/mymorningnews-sdk.js';s=p.read_text();line='        this.'+prop+' = new '+name+'(this, "");\n'
 if line not in s:s=s.replace('        this.account = new AccountResource',line+'        this.account = new AccountResource',1)
 p.write_text(s)
 p=ROOT/'sdk/js/index.d.ts';s=p.read_text();line='    readonly '+prop+': '+name+';\n'
 if line not in s:s=s.replace('    readonly account:',line+'    readonly account:',1)
 p.write_text(s)
 p=ROOT/'sdk/python/mymorningnews_sdk.py';s=p.read_text();line='        self.'+snake(group)+' = '+name+'(self)\n'
 if line not in s:s=s.replace('        self.account = AccountResource',line+'        self.account = AccountResource',1)
 p.write_text(s)
 p=ROOT/'sdk/csharp/MyMorningNews.Sdk/MyMorningNewsClient.cs';s=p.read_text();line='        /// <summary>Platform '+group+' API.</summary>\n        public '+name+' '+group+' { get; }\n\n'
 if line not in s:s=s.replace('        public AccountResource Account { get; }', '        public AccountResource Account { get; }\n\n'+line,1);s=s.replace('            Account = new AccountResource(this);','            Account = new AccountResource(this);\n            '+group+' = new '+name+'(this);',1)
 p.write_text(s)
# New public data types. Existing DTOs are extended separately to preserve the older handwritten contract.
names=['CatalogEntryResponse','CatalogIssueResponse','CatalogResearchResponse','CatalogSubscription','CatalogPublication','BillingStatusResponse','BillingOfferResponse','BillingSubscriptionResponse','BillingRedirectResponse','ProviderConnectionsResponse','ProviderConnection','SignupBenefit','CatalogPublicationRequest','CatalogResearchRequest','CatalogSubscriptionRequest','EntitlementOverrideRequest','BillingCheckoutRequest','BillingPurchaseRequest','ProviderConnectionRequest','FundingModeRequest','BriefingProgressRequest']
typescript=[];python=[];jsdoc=[]
def ts_type(t):
 nullable=t.endswith('?');t=t.rstrip('?')
 if t.startswith('List<'):out=ts_type(t[5:-1])+'[]'
 elif t.startswith('Dictionary<'):out='Record<string, string>'
 else:out={'string':'string','DateTime':'string','bool':'boolean','int':'number','long':'number','double':'number','decimal':'number','JsonObject':'Record<string, unknown>'}.get(t,t if t in names or t in ['Topic','Schedule','EffectivePlan','Run','TopicUpsertRequest','ScheduleUpsertRequest','BriefingComparisonResponse'] else 'unknown')
 return out+(' | null' if nullable else '')
for name in names:
 t=alltypes[name];props=t.props
 typescript.append('/** '+t.summary.replace('\n',' ')+' */\nexport interface '+name+' {')
 python.append('@dataclass\nclass '+name+'(Model):\n    """'+t.summary.replace('\n',' ')+'"""')
 nested={}
 jsdoc.append('/**\n * @typedef {object} '+name)
 for typ,prop,default,doc in props:
  tt=ts_type(typ);typescript.append('    /** '+doc.replace('\n',' ')+' */\n    '+camel(prop)+'?: '+tt+';')
  jsdoc.append(' * @property {'+tt+'} ['+camel(prop)+'] '+doc.replace('\n',' '))
  pytype={'string':'str','DateTime':'str','bool':'bool','int':'int','long':'int','double':'float','decimal':'float'}.get(typ.rstrip('?'),'Any')
  if typ.startswith('List<'): pytype='List[Any]';item=typ[5:-1].rstrip('?');nested[snake(prop)]=item
  elif typ.rstrip('?') in names+['Topic','Schedule','EffectivePlan','BriefingComparisonResponse']:nested[snake(prop)]=typ.rstrip('?')
  python.append('    '+snake(prop)+': Optional['+pytype+'] = None')
 if nested:python.append('    _nested: ClassVar[Dict[str, str]] = '+repr(nested))
 python.append('\n');typescript.append('}\n');jsdoc.append(' */\n')
replace_block(ROOT/'sdk/js/index.d.ts','\n'.join(typescript),'// TYPES','export interface EffectivePlan')
replace_block(ROOT/'sdk/python/mymorningnews_sdk.py','\n'.join(python),'# TYPES','@dataclass\nclass EffectivePlan')
replace_block(ROOT/'sdk/js/mymorningnews-sdk.js','\n'.join(jsdoc),'// TYPES','export class MyMorningNewsError')
