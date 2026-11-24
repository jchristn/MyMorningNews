#!/usr/bin/env python3
"""
Entity code generator for My Morning News.

Reads entity specs from entities.py and writes, for each entity, files that do not already exist:
  MyMorningNews.Core/Models/{Entity}.cs
  MyMorningNews.Core/Requests/{Entity}Filter.cs
  MyMorningNews.Core/Database/Interfaces/I{Entity}Methods.cs
  MyMorningNews.Core/Database/Postgresql/Queries/{Entity}Queries.cs
  MyMorningNews.Core/Database/Postgresql/Implementations/{Entity}Methods.cs
Always regenerates (fully derived files):
  MyMorningNews.Core/IdPrefixes.cs
  MyMorningNews.Core/Helpers/IdGenerator.cs
  MyMorningNews.Core/Enums/*.cs for enums declared in entities.py (only when missing)

Existing entity files are never overwritten, so hand-written additions survive. Pass --force NAME to regenerate one entity.
Usage: python3 codegen.py [--force Entity] [--dry-run]
"""

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.abspath(os.path.join(HERE, '..', '..'))
CORE = os.path.join(SRC, 'MyMorningNews.Core')
sys.path.insert(0, HERE)
import entities  # noqa: E402

FORCE = set()
DRY = '--dry-run' in sys.argv
QUERIES_ONLY = '--queries-only' in sys.argv
for i, a in enumerate(sys.argv):
    if a == '--force' and i + 1 < len(sys.argv):
        FORCE.add(sys.argv[i + 1])

written = []


def write(path, text, always=False, entity=None):
    if QUERIES_ONLY and entity in FORCE and not path.endswith('Queries.cs'):
        return
    if os.path.exists(path) and not always and entity not in FORCE:
        return
    if '\u2014' in text:
        raise SystemExit('em-dash in generated text for ' + path)
    written.append(os.path.relpath(path, SRC))
    if DRY:
        return
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'w', encoding='utf-8') as f:
        f.write(text)


# ---------------------------------------------------------------- field helpers

CS_TYPES = {
    'string': 'string', 'text': 'string', 'int': 'int', 'long': 'long', 'double': 'double', 'decimal': 'decimal',
    'bool': 'bool', 'datetime': 'DateTime', 'strings': 'List<string>', 'ints': 'List<int>', 'time': 'TimeSpan',
}
PG_TYPES = {
    'string': 'VARCHAR({len})', 'text': 'TEXT', 'int': 'INTEGER', 'long': 'BIGINT', 'double': 'DOUBLE PRECISION',
    'decimal': 'NUMERIC(18,6)', 'bool': 'BOOLEAN', 'datetime': 'TIMESTAMPTZ', 'strings': 'TEXT[]', 'ints': 'INTEGER[]',
    'time': 'TIME',
}


class F:
    def __init__(self, d):
        self.name = d['name']
        self.type = d['type']                    # base type key, or 'enum:EnumName'
        self.nullable = d.get('nullable', False)
        self.required = d.get('required', False)  # non-empty string required
        self.default = d.get('default')
        self.clamp = d.get('clamp')              # (min, max)
        self.length = d.get('length', 256)
        self.doc = d.get('doc', self.name + '.')
        self.filter = d.get('filter', False)
        self.sort = d.get('sort', False)
        self.search = d.get('search', False)
        self.index = d.get('index', False)
        self.unique = d.get('unique', False)     # unique within tenant (or global when not tenant scoped)
        self.fk = d.get('fk')                    # (table, onDelete) e.g. ('users', 'CASCADE')
        self.column = d.get('column', self.name.lower())

    @property
    def is_enum(self):
        return self.type.startswith('enum:')

    @property
    def enum_name(self):
        return self.type.split(':', 1)[1]

    @property
    def cs_base(self):
        return self.enum_name if self.is_enum else CS_TYPES[self.type]

    @property
    def cs_type(self):
        t = self.cs_base
        if self.nullable:
            return t + '?'
        return t

    @property
    def pg_type(self):
        if self.is_enum:
            return 'VARCHAR(64)'
        return PG_TYPES[self.type].replace('{len}', str(self.length))

    def cs_default(self):
        if self.default is not None:
            if self.is_enum:
                return self.enum_name + '.' + self.default
            if self.type in ('string', 'text'):
                return 'String.Empty' if self.default == '' else '"' + self.default + '"'
            if self.type == 'bool':
                return 'true' if self.default else 'false'
            if self.type == 'datetime':
                return 'DateTime.UtcNow' if self.default == 'now' else 'null'
            if self.type in ('strings', 'ints'):
                return 'new ' + self.cs_base + '()'
            if self.type == 'time':
                return 'TimeSpan.Zero'
            return str(self.default)
        if self.nullable:
            return 'null'
        if self.type in ('string', 'text'):
            return 'String.Empty'
        if self.type == 'bool':
            return 'false'
        if self.type == 'datetime':
            return 'DateTime.UtcNow'
        if self.type in ('strings', 'ints'):
            return 'new ' + self.cs_base + '()'
        if self.type == 'time':
            return 'TimeSpan.Zero'
        if self.is_enum:
            raise SystemExit('enum field needs default: ' + self.name)
        return '0'

    def needs_backing(self):
        return (self.required and self.type in ('string', 'text')) or self.clamp is not None or \
            (self.type in ('strings', 'ints') and not self.nullable) or \
            (self.type in ('string', 'text') and not self.nullable and not self.required)


def lc(name):
    return name[0].lower() + name[1:]


# ---------------------------------------------------------------- entity

class E:
    def __init__(self, d):
        self.name = d['name']
        self.table = d['table']
        self.prefix = d['prefix']
        self.doc = d['doc']
        self.tenant = d.get('tenant', True)      # tenant scoped
        self.user = d.get('user', False)         # user owned (has userid)
        self.tenant_nullable = d.get('tenant_nullable', False)
        self.parent = d.get('parent')            # extra scope field name used in Enumerate filter (e.g. TopicId)
        self.fields = [F(f) for f in d['fields']]
        self.uniques = d.get('uniques', [])      # list of column lists (composite unique indexes)
        self.indexes = d.get('indexes', [])      # list of column lists
        self.default_sort = d.get('default_sort', 'CreatedUtc')

    def all_fields(self):
        base = [F({'name': 'Id', 'type': 'string', 'length': 64, 'required': True, 'doc': 'Identifier (prefix ' + self.prefix + ').'})]
        if self.tenant:
            base.append(F({'name': 'TenantId', 'type': 'string', 'length': 64, 'required': not self.tenant_nullable,
                           'nullable': self.tenant_nullable, 'doc': 'Tenant identifier.', 'filter': True,
                           'fk': ('tenants', 'CASCADE')}))
        if self.user:
            base.append(F({'name': 'UserId', 'type': 'string', 'length': 64, 'required': True,
                           'doc': 'Owning user identifier.', 'filter': True, 'fk': ('users', 'CASCADE')}))
        tail = [
            F({'name': 'Active', 'type': 'bool', 'default': True, 'doc': 'Whether the record is active.', 'filter': True}),
            F({'name': 'CreatedUtc', 'type': 'datetime', 'default': 'now', 'doc': 'UTC creation time.', 'sort': True}),
            F({'name': 'LastUpdateUtc', 'type': 'datetime', 'default': 'now', 'doc': 'UTC time of the last update.', 'sort': True}),
        ]
        names = set(f.name for f in self.fields)
        tail = [t for t in tail if t.name not in names]
        return base + self.fields + tail


ENTITIES = [E(d) for d in entities.ENTITIES]


# ---------------------------------------------------------------- model

def gen_model(e):
    fields = e.all_fields()
    usings = ['System']
    if any(f.type in ('strings', 'ints') for f in fields):
        usings.append('System.Collections.Generic')
    usings_extra = ['MyMorningNews.Core.Enums', 'MyMorningNews.Core.Helpers']
    lines = []
    lines.append('namespace MyMorningNews.Core.Models')
    lines.append('{')
    for u in sorted(usings):
        lines.append('    using ' + u + ';')
    for u in usings_extra:
        lines.append('    using ' + u + ';')
    lines.append('')
    lines.append('    /// <summary>')
    lines.append('    /// ' + e.doc)
    lines.append('    /// </summary>')
    lines.append('    public class ' + e.name)
    lines.append('    {')
    lines.append('        #region Public-Members')
    lines.append('')
    privates = []
    for f in fields:
        lines.append('        /// <summary>')
        doc = f.doc
        if f.clamp:
            doc += ' Minimum ' + str(f.clamp[0]) + ', maximum ' + str(f.clamp[1]) + '; values outside the range are clamped.'
        if f.nullable:
            doc += ' May be null.'
        lines.append('        /// ' + doc)
        lines.append('        /// </summary>')
        if f.needs_backing():
            pv = '_' + f.name
            if f.required and f.type in ('string', 'text'):
                lines.append('        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>')
                setter = ['if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(' + f.name + '));', pv + ' = value;']
                init = 'IdGenerator.Generate' + e.name + 'Id()' if f.name == 'Id' else f.cs_default()
                if f.name != 'Id' and init == 'String.Empty':
                    init = 'String.Empty'
            elif f.clamp:
                lo, hi = f.clamp
                if f.nullable:
                    setter = [pv + ' = value.HasValue ? Math.Clamp(value.Value, ' + str(lo) + ', ' + str(hi) + ') : null;']
                else:
                    setter = [pv + ' = Math.Clamp(value, ' + str(lo) + ', ' + str(hi) + ');']
                init = f.cs_default()
            elif f.type in ('strings', 'ints'):
                setter = [pv + ' = value ?? new ' + f.cs_base + '();']
                init = f.cs_default()
            else:
                setter = [pv + ' = value ?? String.Empty;']
                init = f.cs_default()
            lines.append('        public ' + f.cs_type + ' ' + f.name)
            lines.append('        {')
            lines.append('            get')
            lines.append('            {')
            lines.append('                return ' + pv + ';')
            lines.append('            }')
            lines.append('            set')
            lines.append('            {')
            for s in setter:
                lines.append('                ' + s)
            lines.append('            }')
            lines.append('        }')
            privates.append('        private ' + f.cs_type + ' ' + pv + ' = ' + init + ';')
        else:
            lines.append('        public ' + f.cs_type + ' ' + f.name + ' { get; set; } = ' + f.cs_default() + ';')
        lines.append('')
    lines.append('        #endregion')
    if privates:
        lines.append('')
        lines.append('        #region Private-Members')
        lines.append('')
        lines.extend(privates)
        lines.append('')
        lines.append('        #endregion')
    lines.append('    }')
    lines.append('}')
    return '\n'.join(lines) + '\n'


# ---------------------------------------------------------------- filter

def gen_filter(e):
    fields = [f for f in e.all_fields() if f.filter and f.name not in ('TenantId',)]
    lines = ['namespace MyMorningNews.Core.Requests', '{', '    using System;', '    using MyMorningNews.Core.Enums;', '']
    lines += ['    /// <summary>', '    /// Enumeration filter for ' + e.name + ' records. Null properties are not applied.', '    /// </summary>',
              '    public class ' + e.name + 'Filter : EnumerationFilter', '    {']
    for f in fields:
        t = f.cs_base
        if f.type in ('strings', 'ints'):
            continue
        lines += ['        /// <summary>', '        /// Match ' + f.name + ' exactly when set.', '        /// </summary>',
                  '        public ' + t + '? ' + f.name + ' { get; set; } = null;', '']
    if lines[-1] == '':
        lines.pop()
    lines += ['    }', '}']
    return '\n'.join(lines) + '\n'


# ---------------------------------------------------------------- interface

def scope_params(e):
    return 'string tenantId, ' if e.tenant else ''


def gen_interface(e):
    n = e.name
    tp = scope_params(e)
    tdoc = ['        /// <param name="tenantId">Tenant identifier; the lookup is constrained to this tenant.</param>'] if e.tenant else []
    L = ['namespace MyMorningNews.Core.Database.Interfaces', '{', '    using System.Collections.Generic;', '    using System.Threading;',
         '    using System.Threading.Tasks;', '    using MyMorningNews.Core.Models;', '    using MyMorningNews.Core.Requests;',
         '    using MyMorningNews.Core.Responses;', '',
         '    /// <summary>', '    /// Data access methods for ' + n + ' records.' + (' Every method is tenant scoped.' if e.tenant else ''),
         '    /// Thread safety: implementations are safe for concurrent use.', '    /// </summary>',
         '    public interface I' + n + 'Methods', '    {']
    L += ['        /// <summary>', '        /// Create a record. An empty identifier is backfilled.', '        /// </summary>',
          '        /// <param name="obj">Record to create.</param>', '        /// <param name="token">Cancellation token.</param>',
          '        /// <returns>Created record.</returns>', '        Task<' + n + '> CreateAsync(' + n + ' obj, CancellationToken token = default);', '']
    L += ['        /// <summary>', '        /// Read a record by identifier.', '        /// </summary>'] + tdoc + [
        '        /// <param name="id">Identifier.</param>', '        /// <param name="token">Cancellation token.</param>',
        '        /// <returns>Record, or null when not found.</returns>',
        '        Task<' + n + '?> ReadAsync(' + tp + 'string id, CancellationToken token = default);', '']
    if e.tenant:
        L += ['        /// <summary>', '        /// Read a record by identifier without tenant scoping. Only for control-plane and internal use.', '        /// </summary>',
              '        /// <param name="id">Identifier.</param>', '        /// <param name="token">Cancellation token.</param>',
              '        /// <returns>Record, or null when not found.</returns>',
              '        Task<' + n + '?> ReadByIdAsync(string id, CancellationToken token = default);', '']
    L += ['        /// <summary>', '        /// Update a record. LastUpdateUtc is set by the implementation.', '        /// </summary>',
          '        /// <param name="obj">Record to update.</param>', '        /// <param name="token">Cancellation token.</param>',
          '        /// <returns>Updated record.</returns>', '        Task<' + n + '> UpdateAsync(' + n + ' obj, CancellationToken token = default);', '']
    L += ['        /// <summary>', '        /// Delete a record. Dependent rows are removed by cascading foreign keys.', '        /// </summary>'] + tdoc + [
        '        /// <param name="id">Identifier.</param>', '        /// <param name="token">Cancellation token.</param>',
        '        /// <returns>True when a record was deleted.</returns>',
        '        Task<bool> DeleteAsync(' + tp + 'string id, CancellationToken token = default);', '']
    if e.tenant:
        L += ['        /// <summary>', '        /// Delete a record by identifier without tenant scoping. Only for control-plane and internal use.', '        /// </summary>',
              '        /// <param name="id">Identifier.</param>', '        /// <param name="token">Cancellation token.</param>',
              '        /// <returns>True when a record was deleted.</returns>',
              '        Task<bool> DeleteByIdAsync(string id, CancellationToken token = default);', '']
    L += ['        /// <summary>', '        /// Check whether a record exists.', '        /// </summary>'] + tdoc + [
        '        /// <param name="id">Identifier.</param>', '        /// <param name="token">Cancellation token.</param>',
        '        /// <returns>True when the record exists.</returns>',
        '        Task<bool> ExistsAsync(' + tp + 'string id, CancellationToken token = default);', '']
    L += ['        /// <summary>', '        /// Enumerate records with paging, sorting, and filters.' + ((' A null TenantId selects global rows; IncludeGlobal adds global rows to a tenant query.' if e.tenant_nullable else ' TenantId on the filter is required.') if e.tenant else ''), '        /// </summary>',
          '        /// <param name="filter">Filter.</param>', '        /// <param name="token">Cancellation token.</param>',
          '        /// <returns>One page of results.</returns>',
          '        Task<EnumerationResult<' + n + '>> EnumerateAsync(' + n + 'Filter filter, CancellationToken token = default);', '']
    L += ['        /// <summary>', '        /// Count records matching a filter.', '        /// </summary>',
          '        /// <param name="filter">Filter.</param>', '        /// <param name="token">Cancellation token.</param>',
          '        /// <returns>Count.</returns>',
          '        Task<long> CountAsync(' + n + 'Filter filter, CancellationToken token = default);']
    L += ['    }', '}']
    return '\n'.join(L) + '\n'


# ---------------------------------------------------------------- queries

def gen_queries(e):
    fields = e.all_fields()
    cols = [f.column for f in fields]
    L = ['namespace MyMorningNews.Core.Database.Postgresql.Queries', '{', '    using System.Collections.Generic;', '',
         '    /// <summary>', '    /// SQL for the ' + e.table + ' table.', '    /// </summary>',
         '    public static class ' + e.name + 'Queries', '    {']
    L += ['        /// <summary>', '        /// Table name.', '        /// </summary>', '        public const string Table = "' + e.table + '";', '']
    L += ['        /// <summary>', '        /// Column list in model order.', '        /// </summary>',
          '        public const string Columns = "' + ', '.join(cols) + '";', '']
    ddl = []
    for f in fields:
        c = '    ' + f.column + ' ' + f.pg_type
        if f.name == 'Id':
            c += ' PRIMARY KEY'
        elif not f.nullable:
            c += ' NOT NULL'
        if f.fk:
            c += ' REFERENCES ' + f.fk[0] + '(id) ON DELETE ' + f.fk[1]
        ddl.append(c)
    stmts = ['CREATE TABLE IF NOT EXISTS ' + e.table + ' (\n' + ',\n'.join(ddl) + '\n)']
    idx_done = set()
    def idx(colset, unique=False):
        key = (tuple(colset), unique)
        if key in idx_done:
            return
        idx_done.add(key)
        name = ('ux_' if unique else 'ix_') + e.table + '_' + '_'.join(colset)
        stmts.append('CREATE ' + ('UNIQUE ' if unique else '') + 'INDEX IF NOT EXISTS ' + name + ' ON ' + e.table + ' (' + ', '.join(colset) + ')')
    if e.tenant:
        idx(['tenantid'])
    if e.user:
        idx(['tenantid', 'userid'])
    for f in fields:
        if f.unique:
            idx((['tenantid'] if e.tenant else []) + [f.column], True)
        elif f.index or f.fk and f.name not in ('TenantId', 'UserId'):
            idx([f.column])
    for u in e.uniques:
        idx(u, True)
    for i in e.indexes:
        idx(i)
    idx(['createdutc'])
    L += ['        /// <summary>', '        /// Schema statements: table and indexes. Idempotent.', '        /// </summary>',
          '        public static readonly IReadOnlyList<string> Schema = new List<string>', '        {']
    for s in stmts:
        L.append('            @"' + s.replace('"', '""') + '",')
    L[-1] = L[-1].rstrip(',')
    L += ['        };', '']
    L += ['        /// <summary>', '        /// Insert statement with positional parameters in column order.', '        /// </summary>',
          '        public const string Insert = "INSERT INTO ' + e.table + ' (" + Columns + ") VALUES (' +
          ', '.join('$' + str(i + 1) for i in range(len(cols))) + ')";', '']
    upd_fields = [f for f in fields if f.name not in ('Id', 'TenantId', 'CreatedUtc')]
    where = 'id = $1' + ((' AND tenantid IS NOT DISTINCT FROM $2' if e.tenant_nullable else ' AND tenantid = $2') if e.tenant else '')
    start = 3 if e.tenant else 2
    sets = ', '.join(f.column + ' = $' + str(start + i) for i, f in enumerate(upd_fields))
    L += ['        /// <summary>', '        /// Update statement. $1 is id' + (', $2 is tenantid' if e.tenant else '') + ', then the mutable columns in model order.', '        /// </summary>',
          '        public const string Update = "UPDATE ' + e.table + ' SET ' + sets + ' WHERE ' + where + '";', '']
    L += ['        /// <summary>', '        /// Select by identifier' + (' within a tenant' if e.tenant else '') + '.', '        /// </summary>',
          '        public const string SelectById = "SELECT " + Columns + " FROM ' + e.table + ' WHERE ' + where + '";', '']
    if e.tenant:
        L += ['        /// <summary>', '        /// Select by identifier without tenant scoping.', '        /// </summary>',
              '        public const string SelectByIdUnscoped = "SELECT " + Columns + " FROM ' + e.table + ' WHERE id = $1";', '']
    L += ['        /// <summary>', '        /// Delete by identifier' + (' within a tenant' if e.tenant else '') + '.', '        /// </summary>',
          '        public const string Delete = "DELETE FROM ' + e.table + ' WHERE ' + where + '";', '']
    if e.tenant:
        L += ['        /// <summary>', '        /// Delete by identifier without tenant scoping.', '        /// </summary>',
              '        public const string DeleteUnscoped = "DELETE FROM ' + e.table + ' WHERE id = $1";', '']
    L += ['        /// <summary>', '        /// Existence check.', '        /// </summary>',
          '        public const string Exists = "SELECT 1 FROM ' + e.table + ' WHERE ' + where + ' LIMIT 1";']
    L += ['    }', '}']
    return '\n'.join(L) + '\n'


# ---------------------------------------------------------------- implementation

def read_expr(f, idx):
    t = f.type
    o = 'reader'
    if f.is_enum:
        if f.nullable:
            return '(' + o + '.IsDBNull(' + str(idx) + ') ? null : Enum.Parse<' + f.enum_name + '>(' + o + '.GetString(' + str(idx) + ')))'
        return 'Enum.Parse<' + f.enum_name + '>(' + o + '.GetString(' + str(idx) + '))'
    getter = {
        'string': 'GetString', 'text': 'GetString', 'int': 'GetInt32', 'long': 'GetInt64', 'double': 'GetDouble',
        'decimal': 'GetDecimal', 'bool': 'GetBoolean', 'datetime': 'GetDateTime', 'time': 'GetTimeSpan',
    }
    if t in ('strings', 'ints'):
        el = 'string' if t == 'strings' else 'int'
        expr = 'new List<' + el + '>(' + o + '.GetFieldValue<' + el + '[]>(' + str(idx) + '))'
        if f.nullable:
            return '(' + o + '.IsDBNull(' + str(idx) + ') ? null : ' + expr + ')'
        return '(' + o + '.IsDBNull(' + str(idx) + ') ? new List<' + el + '>() : ' + expr + ')'
    g = o + '.' + getter[t] + '(' + str(idx) + ')'
    if t == 'datetime':
        g = 'DateTime.SpecifyKind(' + g + ', DateTimeKind.Utc)'
    if f.nullable:
        if t == 'string' or t == 'text':
            return '(' + o + '.IsDBNull(' + str(idx) + ') ? null : ' + g + ')'
        return '(' + o + '.IsDBNull(' + str(idx) + ') ? (' + f.cs_type + ')null : ' + g + ')'
    if t in ('string', 'text'):
        return '(' + o + '.IsDBNull(' + str(idx) + ') ? String.Empty : ' + g + ')'
    return g


def param_expr(f, var):
    v = var + '.' + f.name
    if f.is_enum:
        if f.nullable:
            return '(object?)' + v + '?.ToString()'
        return v + '.ToString()'
    if f.type in ('strings', 'ints'):
        return v + (' == null ? null : ' + v + '.ToArray()' if f.nullable else '.ToArray()')
    if f.type == 'datetime':
        if f.nullable:
            return v + '.HasValue ? DateTime.SpecifyKind(' + v + '.Value, DateTimeKind.Utc) : null'
        return 'DateTime.SpecifyKind(' + v + ', DateTimeKind.Utc)'
    return v


def gen_impl(e):
    n = e.name
    fields = e.all_fields()
    tp = scope_params(e)
    q = n + 'Queries'
    sortable = [f for f in fields if f.sort] or [f for f in fields if f.name == 'CreatedUtc']
    filters = [f for f in fields if f.filter and f.type not in ('strings', 'ints')]
    searchable = [f for f in fields if f.search]
    L = ['namespace MyMorningNews.Core.Database.Postgresql.Implementations', '{',
         '    using System;', '    using System.Collections.Generic;', '    using System.Diagnostics;', '    using System.Linq;', '    using System.Text;',
         '    using System.Threading;', '    using System.Threading.Tasks;',
         '    using MyMorningNews.Core.Database.Interfaces;', '    using MyMorningNews.Core.Database.Postgresql.Queries;',
         '    using MyMorningNews.Core.Enums;', '    using MyMorningNews.Core.Helpers;', '    using MyMorningNews.Core.Models;',
         '    using MyMorningNews.Core.Requests;', '    using MyMorningNews.Core.Responses;', '    using Npgsql;', '',
         '    /// <summary>', '    /// PostgreSQL implementation of <see cref="I' + n + 'Methods"/>.', '    /// Thread safety: stateless apart from the shared data source; safe for concurrent use.', '    /// </summary>',
         '    public class ' + n + 'Methods : PostgresqlMethodsBase, I' + n + 'Methods', '    {',
         '        #region Private-Members', '',
         '        private static readonly Dictionary<string, string> _SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)',
         '        {']
    for f in sortable:
        L.append('            { "' + lc(f.name) + '", "' + f.column + '" },')
    L[-1] = L[-1].rstrip(',')
    L += ['        };', '', '        #endregion', '', '        #region Constructors-and-Factories', '',
          '        /// <summary>', '        /// Instantiate.', '        /// </summary>', '        /// <param name="driver">PostgreSQL driver.</param>',
          '        public ' + n + 'Methods(PostgresqlDatabaseDriver driver) : base(driver)', '        {', '        }', '',
          '        #endregion', '', '        #region Public-Methods', '']
    # Create
    L += ['        /// <inheritdoc />', '        public async Task<' + n + '> CreateAsync(' + n + ' obj, CancellationToken token = default)', '        {',
          '            ArgumentNullException.ThrowIfNull(obj);',
          '            if (String.IsNullOrWhiteSpace(obj.Id)) obj.Id = IdGenerator.Generate' + n + 'Id();',
          '            DateTime now = DateTime.UtcNow;', '            obj.CreatedUtc = now;', '            obj.LastUpdateUtc = now;',
          '            await ExecuteAsync(' + q + '.Insert, token,']
    L.append(',\n'.join('                ' + param_expr(f, 'obj') for f in fields) + ').ConfigureAwait(false);')
    L += ['            return obj;', '        }', '']
    # Read
    rargs = ('tenantId, id' if e.tenant else 'id')
    L += ['        /// <inheritdoc />', '        public async Task<' + n + '?> ReadAsync(' + tp + 'string id, CancellationToken token = default)', '        {']
    if e.tenant:
        L.append('            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));')
    L += ['            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));',
          '            List<' + n + '> rows = await QueryAsync(' + q + '.SelectById, Map, token, id' + (', tenantId' if e.tenant else '') + ').ConfigureAwait(false);',
          '            return rows.FirstOrDefault();', '        }', '']
    if e.tenant:
        L += ['        /// <inheritdoc />', '        public async Task<' + n + '?> ReadByIdAsync(string id, CancellationToken token = default)', '        {',
              '            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));',
              '            List<' + n + '> rows = await QueryAsync(' + q + '.SelectByIdUnscoped, Map, token, id).ConfigureAwait(false);',
              '            return rows.FirstOrDefault();', '        }', '']
    # Update
    upd = [f for f in fields if f.name not in ('Id', 'TenantId', 'CreatedUtc')]
    L += ['        /// <inheritdoc />', '        public async Task<' + n + '> UpdateAsync(' + n + ' obj, CancellationToken token = default)', '        {',
          '            ArgumentNullException.ThrowIfNull(obj);', '            obj.LastUpdateUtc = DateTime.UtcNow;',
          '            int affected = await ExecuteAsync(' + q + '.Update, token,',
          '                obj.Id,']
    if e.tenant:
        L.append('                (object?)obj.TenantId,')
    L.append(',\n'.join('                ' + param_expr(f, 'obj') for f in upd) + ').ConfigureAwait(false);')
    L += ['            if (affected < 1) throw new KeyNotFoundException("' + n + ' " + obj.Id + " was not found.");', '            return obj;', '        }', '']
    # Delete / Exists
    for m, qq, ret in (('DeleteAsync', 'Delete', 'bool'), ('ExistsAsync', 'Exists', 'bool')):
        L += ['        /// <inheritdoc />', '        public async Task<bool> ' + m + '(' + tp + 'string id, CancellationToken token = default)', '        {']
        if e.tenant:
            L.append('            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));')
        L.append('            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));')
        if m == 'DeleteAsync':
            L.append('            int affected = await ExecuteAsync(' + q + '.Delete, token, id' + (', tenantId' if e.tenant else '') + ').ConfigureAwait(false);')
            L.append('            return affected > 0;')
        else:
            L.append('            List<int> rows = await QueryAsync(' + q + '.Exists, r => 1, token, id' + (', tenantId' if e.tenant else '') + ').ConfigureAwait(false);')
            L.append('            return rows.Count > 0;')
        L += ['        }', '']
    if e.tenant:
        L += ['        /// <inheritdoc />', '        public async Task<bool> DeleteByIdAsync(string id, CancellationToken token = default)', '        {',
              '            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));',
              '            int affected = await ExecuteAsync(' + q + '.DeleteUnscoped, token, id).ConfigureAwait(false);',
              '            return affected > 0;', '        }', '']
    # Enumerate
    L += ['        /// <inheritdoc />', '        public async Task<EnumerationResult<' + n + '>> EnumerateAsync(' + n + 'Filter filter, CancellationToken token = default)', '        {',
          '            ArgumentNullException.ThrowIfNull(filter);', '            Stopwatch sw = Stopwatch.StartNew();',
          '            List<object?> args = new List<object?>();', '            string where = BuildWhere(filter, args);',
          '            string sortColumn = _SortColumns.TryGetValue(filter.SortBy ?? String.Empty, out string? col) ? col : "' + [f for f in fields if f.name == e.default_sort][0].column + '";',
          '            string direction = filter.SortDirection == SortDirectionEnum.Ascending ? "ASC" : "DESC";',
          '            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + ' + q + '.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);',
          '            long total = counts.FirstOrDefault();',
          '            args.Add(filter.PageSize);', '            args.Add((long)(filter.PageNumber - 1) * filter.PageSize);',
          '            string sql = "SELECT " + ' + q + '.Columns + " FROM " + ' + q + '.Table + where + " ORDER BY " + sortColumn + " " + direction + ", id " + direction + " LIMIT $" + (args.Count - 1) + " OFFSET $" + args.Count;',
          '            List<' + n + '> items = await QueryAsync(sql, Map, token, args.ToArray()).ConfigureAwait(false);',
          '            return EnumerationResult<' + n + '>.Create(items, filter, total, sw.Elapsed.TotalMilliseconds);', '        }', '']
    L += ['        /// <inheritdoc />', '        public async Task<long> CountAsync(' + n + 'Filter filter, CancellationToken token = default)', '        {',
          '            ArgumentNullException.ThrowIfNull(filter);', '            List<object?> args = new List<object?>();',
          '            string where = BuildWhere(filter, args);',
          '            List<long> counts = await QueryAsync("SELECT COUNT(*) FROM " + ' + q + '.Table + where, r => r.GetInt64(0), token, args.ToArray()).ConfigureAwait(false);',
          '            return counts.FirstOrDefault();', '        }', '']
    # Map
    L += ['        /// <summary>', '        /// Map a data reader row (columns in <see cref="' + q + '.Columns"/> order) to a model.', '        /// </summary>',
          '        /// <param name="reader">Reader positioned on a row.</param>', '        /// <returns>Model.</returns>',
          '        public static ' + n + ' Map(NpgsqlDataReader reader)', '        {', '            ArgumentNullException.ThrowIfNull(reader);',
          '            ' + n + ' obj = new ' + n + '();']
    for i, f in enumerate(fields):
        L.append('            obj.' + f.name + ' = ' + read_expr(f, i) + ';')
    L += ['            return obj;', '        }', '', '        #endregion', '', '        #region Private-Methods', '',
          '        private static string BuildWhere(' + n + 'Filter filter, List<object?> args)', '        {',
          '            List<string> clauses = new List<string>();']
    if e.tenant and not e.tenant_nullable:
        L += ['            if (String.IsNullOrWhiteSpace(filter.TenantId)) throw new ArgumentNullException(nameof(filter) + ".TenantId");',
              '            args.Add(filter.TenantId);', '            clauses.Add("tenantid = $" + args.Count);']
    elif e.tenant and e.tenant_nullable:
        L += ['            if (filter.AllTenants)', '            {', '            }', '            else if (String.IsNullOrWhiteSpace(filter.TenantId))', '            {', '                clauses.Add("tenantid IS NULL");', '            }',
              '            else', '            {', '                args.Add(filter.TenantId);',
              '                clauses.Add(filter.IncludeGlobal ? "(tenantid = $" + args.Count + " OR tenantid IS NULL)" : "tenantid = $" + args.Count);', '            }']
    for f in filters:
        if f.name == 'TenantId':
            continue
        v = 'filter.' + f.name
        if f.is_enum:
            L += ['            if (' + v + '.HasValue)', '            {', '                args.Add(' + v + '.Value.ToString());',
                  '                clauses.Add("' + f.column + ' = $" + args.Count);', '            }']
        elif f.type in ('string', 'text'):
            L += ['            if (!String.IsNullOrWhiteSpace(' + v + '))', '            {', '                args.Add(' + v + ');',
                  '                clauses.Add("' + f.column + ' = $" + args.Count);', '            }']
        else:
            L += ['            if (' + v + '.HasValue)', '            {', '                args.Add(' + v + '.Value);',
                  '                clauses.Add("' + f.column + ' = $" + args.Count);', '            }']
    if searchable:
        L += ['            if (!String.IsNullOrWhiteSpace(filter.Search))', '            {',
              '                args.Add("%" + EscapeLike(filter.Search.Trim()) + "%");',
              '                clauses.Add("(' + ' OR '.join(f.column + ' ILIKE $" + args.Count + "' for f in searchable) + ')");', '            }']
    L += ['            if (filter.FromUtc.HasValue)', '            {', '                args.Add(DateTime.SpecifyKind(filter.FromUtc.Value, DateTimeKind.Utc));',
          '                clauses.Add("createdutc >= $" + args.Count);', '            }',
          '            if (filter.ToUtc.HasValue)', '            {', '                args.Add(DateTime.SpecifyKind(filter.ToUtc.Value, DateTimeKind.Utc));',
          '                clauses.Add("createdutc < $" + args.Count);', '            }',
          '            return clauses.Count > 0 ? " WHERE " + String.Join(" AND ", clauses) : String.Empty;', '        }', '',
          '        #endregion', '    }', '}']
    return '\n'.join(L) + '\n'


# ---------------------------------------------------------------- id generator, prefixes, enums

def gen_prefixes():
    L = ['namespace MyMorningNews.Core', '{', '    /// <summary>', '    /// Identifier prefixes for every entity. Generated by src/Tools/codegen.', '    /// </summary>',
         '    public static class IdPrefixes', '    {']
    for e in ENTITIES:
        L += ['        /// <summary>', '        /// Prefix for ' + e.name + ' identifiers.', '        /// </summary>', '        public const string ' + e.name + ' = "' + e.prefix + '";', '']
    for name, pfx, doc in entities.EXTRA_PREFIXES:
        L += ['        /// <summary>', '        /// ' + doc, '        /// </summary>', '        public const string ' + name + ' = "' + pfx + '";', '']
    L.pop()
    L += ['    }', '}']
    return '\n'.join(L) + '\n'


def gen_idgen():
    L = ['namespace MyMorningNews.Core.Helpers', '{', '    using System;', '', '    /// <summary>',
         '    /// Generates K-sortable, prefixed identifiers with PrettyId. Generated by src/Tools/codegen.',
         '    /// Thread safety: generation is thread safe.', '    /// </summary>', '    public static class IdGenerator', '    {',
         '        #region Public-Members', '', '        /// <summary>', '        /// Identifier length including the prefix. Default 32.', '        /// </summary>',
         '        public static int IdLength { get; } = 32;', '', '        #endregion', '', '        #region Private-Members', '',
         '        private static readonly PrettyId.IdGenerator _Generator = new PrettyId.IdGenerator();', '', '        #endregion', '',
         '        #region Public-Methods', '',
         '        /// <summary>', '        /// Generate an identifier with an arbitrary prefix.', '        /// </summary>',
         '        /// <param name="prefix">Prefix such as "top_".</param>', '        /// <returns>Identifier.</returns>',
         '        /// <exception cref="ArgumentNullException">prefix is null or empty.</exception>',
         '        public static string Generate(string prefix)', '        {', '            if (String.IsNullOrWhiteSpace(prefix)) throw new ArgumentNullException(nameof(prefix));',
         '            return _Generator.GenerateKSortable(prefix, IdLength);', '        }', '']
    for e in ENTITIES:
        L += ['        /// <summary>', '        /// Generate a ' + e.name + ' identifier.', '        /// </summary>', '        /// <returns>Identifier.</returns>',
              '        public static string Generate' + e.name + 'Id()', '        {', '            return Generate(IdPrefixes.' + e.name + ');', '        }', '']
    for name, pfx, doc in entities.EXTRA_PREFIXES:
        L += ['        /// <summary>', '        /// Generate a ' + name + ' identifier.', '        /// </summary>', '        /// <returns>Identifier.</returns>',
              '        public static string Generate' + name + 'Id()', '        {', '            return Generate(IdPrefixes.' + name + ');', '        }', '']
    L.pop()
    L += ['', '        #endregion', '    }', '}']
    return '\n'.join(L) + '\n'


def gen_enum(name, doc, values):
    L = ['namespace MyMorningNews.Core.Enums', '{', '    /// <summary>', '    /// ' + doc, '    /// </summary>', '    public enum ' + name, '    {']
    for v, vdoc in values:
        L += ['        /// <summary>', '        /// ' + vdoc, '        /// </summary>', '        ' + v + ',', '']
    L.pop()
    L[-1] = L[-1].rstrip(',')
    L += ['    }', '}']
    return '\n'.join(L) + '\n'


def main():
    for e in ENTITIES:
        write(os.path.join(CORE, 'Models', e.name + '.cs'), gen_model(e), entity=e.name)
        write(os.path.join(CORE, 'Requests', e.name + 'Filter.cs'), gen_filter(e), entity=e.name)
        write(os.path.join(CORE, 'Database', 'Interfaces', 'I' + e.name + 'Methods.cs'), gen_interface(e), entity=e.name)
        write(os.path.join(CORE, 'Database', 'Postgresql', 'Queries', e.name + 'Queries.cs'), gen_queries(e), entity=e.name)
        write(os.path.join(CORE, 'Database', 'Postgresql', 'Implementations', e.name + 'Methods.cs'), gen_impl(e), entity=e.name)
    write(os.path.join(CORE, 'IdPrefixes.cs'), gen_prefixes(), always=True)
    write(os.path.join(CORE, 'Helpers', 'IdGenerator.cs'), gen_idgen(), always=True)
    for name, doc, values in entities.ENUMS:
        write(os.path.join(CORE, 'Enums', name + '.cs'), gen_enum(name, doc, values), entity=name)
    for w in written:
        print('wrote ' + w)


if __name__ == '__main__':
    main()
