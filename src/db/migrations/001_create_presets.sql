-- D1 migration: create presets table
CREATE TABLE IF NOT EXISTS presets (
	id INTEGER PRIMARY KEY AUTOINCREMENT,
	name TEXT NOT NULL,
	creator TEXT NOT NULL,
	config_json TEXT NOT NULL,
	version TEXT DEFAULT '1',
	tags TEXT, -- comma-separated tags, e.g. "闇鍋,人外"
	downloads INTEGER DEFAULT 0,
	created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);
