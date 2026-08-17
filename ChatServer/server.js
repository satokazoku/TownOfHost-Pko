const WebSocket = require('ws');
const PORT = 8081;
const wss = new WebSocket.Server({ port: PORT });
const allClients = new Set();

const Sep = '\x1E';
const KindChat = "CHAT";
const KindLink = "LINK";

wss.on('connection', (ws) => {
    allClients.add(ws);
    ws.myLinkId = null;
    ws.linkedIds = new Set();

    ws.on('message', (data) => {
        const msg = data.toString().trim();
        if (!msg) return;

        const parts = msg.split(Sep);
        if (parts.length < 2) return;

        const kind = parts[0];
        const fromId = parts[1];

        if (!ws.myLinkId && fromId) {
            for (const client of allClients) {
                if (client.myLinkId === fromId) {
                    ws.close();
                    return;
                }
            }
            ws.myLinkId = fromId;
        }

        if (ws.myLinkId !== fromId) return;

        if (kind === KindLink) {
            const toId = parts[2];
            if (!toId) return;

            ws.linkedIds.add(toId);

            for (const client of allClients) {
                if (client.myLinkId === toId && client.readyState === WebSocket.OPEN) {
                    client.send(msg);
                }
            }
        }
        else if (kind === KindChat) {
            for (const client of allClients) {
                if (client === ws) continue;

                if (ws.linkedIds.has(client.myLinkId) && client.linkedIds.has(ws.myLinkId)) {
                    if (client.readyState === WebSocket.OPEN) {
                        client.send(msg);
                    }
                }
            }
        }
    });

    ws.on('close', () => {
        allClients.delete(ws);
    });

    ws.on('error', () => { });
});