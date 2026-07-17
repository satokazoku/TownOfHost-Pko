// Cloudflare Worker for presets API (D1)
export default {
  async fetch(request: Request, env: any) {
    try {
      const url = new URL(request.url);
      const pathname = url.pathname.replace(/\/+$/, '');

      if (request.method === 'OPTIONS') return new Response(null, { status: 204 });

      // POST /presets - create a new preset
      if (request.method === 'POST' && pathname === '/presets') {
        const contentType = request.headers.get('content-type') || '';
        if (!contentType.includes('application/json')) {
          return new Response(JSON.stringify({ error: 'application/json required' }), { status: 400 });
        }
        const body = await request.json();
        const name = (body.name || '').toString().trim();
        const creator = (body.creator || '').toString().trim();
        const config_json = (body.config_json || '').toString();
        const version = (body.version || '1').toString();
        const tags = Array.isArray(body.tags) ? body.tags.map(String).join(',') : (body.tags || '').toString();

        if (!name || !creator || !config_json) {
          return new Response(JSON.stringify({ error: 'name, creator, config_json are required' }), { status: 400 });
        }

        const sql = `INSERT INTO presets (name, creator, config_json, version, tags) VALUES (?, ?, ?, ?, ?)`;
        const result = await env.D1_PRESETS.prepare(sql).bind(name, creator, config_json, version, tags).run();
        // D1 run returns meta with last_row_id in many environments
        const id = result?.meta?.last_row_id ?? result?.meta?.last_insert_rowid ?? null;

        return new Response(JSON.stringify({ id }), {
          status: 201,
          headers: { 'content-type': 'application/json' },
        });
      }

      // GET /presets - list with filters, sort, paging
      if (request.method === 'GET' && pathname === '/presets') {
        const params = url.searchParams;
        const tagsParam = params.get('tags') || ''; // comma-separated
        const versionParam = params.get('version');
        const sort = (params.get('sort') || 'new').toLowerCase(); // 'popular' or 'new'
        const page = Math.max(1, parseInt(params.get('page') || '1'));
        const per_page = Math.min(100, Math.max(1, parseInt(params.get('per_page') || '20')));
        const q = params.get('q') || '';

        const where: string[] = [];
        const binds: any[] = [];

        if (tagsParam) {
          const tags = tagsParam.split(',').map(t => t.trim()).filter(Boolean);
          if (tags.length > 0) {
            const tagClauses = tags.map(_ => `tags LIKE '%' || ? || '%'`);
            where.push('(' + tagClauses.join(' OR ') + ')');
            binds.push(...tags);
          }
        }

        if (versionParam) {
          where.push('version = ?');
          binds.push(versionParam);
        }

        if (q) {
          where.push('(name LIKE '%' || ? || '%' OR creator LIKE '%' || ? || '%')');
          binds.push(q, q);
        }

        const whereSql = where.length ? ('WHERE ' + where.join(' AND ')) : '';

        const orderSql = sort === 'popular' ? 'ORDER BY downloads DESC' : 'ORDER BY created_at DESC';
        const offset = (page - 1) * per_page;

        const selectSql = `SELECT id, name, creator, version, tags, downloads, created_at FROM presets ${whereSql} ${orderSql} LIMIT ? OFFSET ?`;
        const allBinds = binds.concat([per_page, offset]);

        const listResult = await env.D1_PRESETS.prepare(selectSql).bind(...allBinds).all();
        const rows = listResult?.results ?? listResult;

        // total count
        const countSql = `SELECT COUNT(*) as total FROM presets ${whereSql}`;
        const countResult = await env.D1_PRESETS.prepare(countSql).bind(...binds).all();
        const total = Number(countResult?.results?.[0]?.total ?? countResult?.results?.[0]?.['COUNT(*)'] ?? 0);

        return new Response(JSON.stringify({ total, page, per_page, presets: rows }), {
          status: 200,
          headers: { 'content-type': 'application/json' },
        });
      }

      // GET /presets/:id
      const presetIdMatch = pathname.match(/^\/presets\/(\d+)$/);
      if (request.method === 'GET' && presetIdMatch) {
        const id = Number(presetIdMatch[1]);
        const row = await env.D1_PRESETS.prepare('SELECT id, name, creator, version, tags, downloads, created_at FROM presets WHERE id = ?').bind(id).all();
        const resultRow = row?.results?.[0] ?? null;
        if (!resultRow) return new Response(JSON.stringify({ error: 'not found' }), { status: 404 });
        return new Response(JSON.stringify(resultRow), { status: 200, headers: { 'content-type': 'application/json' } });
      }

      // POST /presets/:id/download
      const downloadMatch = pathname.match(/^\/presets\/(\d+)\/download$/);
      if (request.method === 'POST' && downloadMatch) {
        const id = Number(downloadMatch[1]);
        // increment downloads
        await env.D1_PRESETS.prepare('UPDATE presets SET downloads = downloads + 1 WHERE id = ?').bind(id).run();
        const row = await env.D1_PRESETS.prepare('SELECT config_json FROM presets WHERE id = ?').bind(id).all();
        const configRow = row?.results?.[0] ?? null;
        if (!configRow) return new Response(JSON.stringify({ error: 'not found' }), { status: 404 });
        // return config_json and metadata
        return new Response(JSON.stringify({ id, config_json: configRow.config_json }), { status: 200, headers: { 'content-type': 'application/json' } });
      }

      return new Response('Not found', { status: 404 });
    } catch (err: any) {
      return new Response(JSON.stringify({ error: String(err) }), { status: 500 });
    }
  }
};
