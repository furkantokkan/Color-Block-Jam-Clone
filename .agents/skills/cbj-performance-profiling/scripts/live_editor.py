"""Talks to the live Unity Editor through the MCP for Unity HTTP bridge (http://127.0.0.1:8082/mcp).

The bridge serves every Editor that has the MCP for Unity package in HTTP mode, so this client pins the
ColorBlockJamCase2026 instance by name before each call (another project, BRN-Game-2, is often connected too).

Usage:
  python live_editor.py instances
  python live_editor.py code <body.cs> [--set KEY=VALUE ...]   # runs a C# method body via execute_code
  python live_editor.py tool <tool_name> <args.json>             # any other MCP tool
  python live_editor.py resource <uri>

--set replaces __KEY__ tokens in the C# body (for example --set FRAMES=600 --set PROFILE_EDITOR=true).
Standard library only. Prints the returned string (or JSON) and exits 1 when the call failed.
"""
import argparse
import json
import sys
import urllib.error
import urllib.request

URL = "http://127.0.0.1:8082/mcp"
HEADERS = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}
PROJECT_NAME = "ColorBlockJamCase2026"


def post(body, session=None, timeout=600):
    headers = dict(HEADERS)
    if session:
        headers["mcp-session-id"] = session
    request = urllib.request.Request(URL, data=json.dumps(body).encode("utf-8"), headers=headers, method="POST")
    with urllib.request.urlopen(request, timeout=timeout) as response:
        session_id = response.headers.get("mcp-session-id")
        text = response.read().decode("utf-8", "replace")
    message = None
    # The SSE body can carry notifications before the response; keep the data line that has an id.
    for line in text.splitlines():
        if line.startswith("data:"):
            data = json.loads(line[5:])
            if "id" in data:
                message = data
    if message is None and text.strip().startswith("{"):
        message = json.loads(text)
    return session_id, message


def open_session():
    session, _ = post({"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {
        "protocolVersion": "2025-03-26", "capabilities": {}, "clientInfo": {"name": "cbj-performance-profiling", "version": "1"}}})
    post({"jsonrpc": "2.0", "method": "notifications/initialized"}, session)
    return session


def read_resource(session, uri):
    _, message = post({"jsonrpc": "2.0", "id": 2, "method": "resources/read", "params": {"uri": uri}}, session)
    contents = (message or {}).get("result", {}).get("contents", [])
    return contents[0].get("text", "") if contents else json.dumps(message)


def find_instance(session):
    data = json.loads(read_resource(session, "mcpforunity://instances"))
    for instance in data.get("instances", []):
        if instance.get("name") == PROJECT_NAME:
            return instance["id"]
    names = [i.get("id") for i in data.get("instances", [])]
    raise SystemExit(f"{PROJECT_NAME} is not connected to the 8082 bridge (connected: {names}). "
                     "Open MCP for Unity in the Editor, set Transport to HTTPLocal on port 8082 and start the server.")


def call_tool(session, name, arguments):
    _, message = post({"jsonrpc": "2.0", "id": 3, "method": "tools/call", "params": {"name": name, "arguments": arguments}}, session)
    result = (message or {}).get("result", {})
    content = result.get("structuredContent", message)
    data = content.get("data") if isinstance(content, dict) else None
    if isinstance(data, dict) and "result" in data:
        return True, data["result"] if isinstance(data["result"], str) else json.dumps(data["result"], indent=1)
    ok = isinstance(content, dict) and content.get("success", False)
    return ok, json.dumps(content, indent=1)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("instances")
    code = sub.add_parser("code")
    code.add_argument("body")
    code.add_argument("--set", action="append", default=[], metavar="KEY=VALUE")
    tool = sub.add_parser("tool")
    tool.add_argument("name")
    tool.add_argument("args")
    resource = sub.add_parser("resource")
    resource.add_argument("uri")
    options = parser.parse_args()

    try:
        run(options)
    except (urllib.error.URLError, ConnectionError) as error:
        # The bridge restarts when the MCP server or the Editor reloads; wait and retry instead of changing anything.
        raise SystemExit(f"The 8082 bridge is not reachable ({error}). Check that the MCP for Unity server runs in "
                         "HTTPLocal mode, wait for any Editor reload to finish, then retry.")


def run(options):
    session = open_session()
    if options.command == "instances":
        print(read_resource(session, "mcpforunity://instances"))
        return
    if options.command == "resource":
        print(read_resource(session, options.uri))
        return

    instance = find_instance(session)
    if options.command == "code":
        body = open(options.body, encoding="utf-8").read()
        for pair in options.set:
            key, _, value = pair.partition("=")
            body = body.replace(f"__{key}__", value)
        ok, text = call_tool(session, "execute_code", {"unity_instance": instance, "action": "execute", "code": body})
    else:
        arguments = json.load(open(options.args, encoding="utf-8"))
        arguments.setdefault("unity_instance", instance)
        ok, text = call_tool(session, options.name, arguments)
    print(text)
    if not ok:
        sys.exit(1)


if __name__ == "__main__":
    main()
