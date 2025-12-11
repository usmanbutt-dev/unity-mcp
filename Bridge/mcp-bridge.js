const http = require('http');

const UNITY_PORT = process.env.UNITY_MCP_PORT || 3000;
const UNITY_HOST = process.env.UNITY_MCP_HOST || 'localhost';

// Enhanced logging to stderr (so it doesn't interfere with stdout JSON-RPC)
function log(msg) {
    console.error(`[Unity MCP Bridge] ${msg}`);
}

// 1. Handle Stdin -> POST to Unity
const readline = require('readline');
const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
    terminal: false
});

rl.on('line', (line) => {
    if (!line.trim()) return;

    const req = http.request({
        hostname: UNITY_HOST,
        port: UNITY_PORT,
        path: '/message',
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'Content-Length': Buffer.byteLength(line)
        }
    }, (res) => {
        let data = '';
        res.on('data', chunk => data += chunk);
        res.on('end', () => {
            if (res.statusCode !== 200) {
                log(`Unity returned error ${res.statusCode}: ${data}`);
            } else if (data) {
                // Forward Unity's response to stdout for the MCP client
                console.log(data);
            }
        });
    });

    req.on('error', (e) => {
        log(`Error posting to Unity: ${e.message}`);
    });

    req.write(line);
    req.end();
});

// 2. Handle SSE from Unity -> Stdout
function connectSSE() {
    log(`Connecting to Unity at http://${UNITY_HOST}:${UNITY_PORT}/sse`);
    
    const req = http.request({
        hostname: UNITY_HOST,
        port: UNITY_PORT,
        path: '/sse',
        method: 'GET',
        headers: {
            'Accept': 'text/event-stream'
        }
    }, (res) => {
        if (res.statusCode !== 200) {
            log(`Failed to connect to SSE. Status: ${res.statusCode}`);
            setTimeout(connectSSE, 5000);
            return;
        }

        log('Connected to Unity MCP Server');

        res.on('data', (chunk) => {
            const text = chunk.toString();
            // SSE format: "data: {json}\n\n"
            const lines = text.split('\n');
            for (const line of lines) {
                if (line.startsWith('data: ')) {
                    const json = line.substring(6).trim();
                    if (json) {
                        // Forward to MCP client via stdout
                        console.log(json);
                    }
                }
            }
        });

        res.on('end', () => {
            log('SSE Connection closed. Reconnecting...');
            setTimeout(connectSSE, 1000);
        });
    });

    req.on('error', (e) => {
        setTimeout(connectSSE, 5000);
    });

    req.end();
}

// Start SSE listener
connectSSE();

log('Bridge started. Waiting for MCP client input...');
