"""RudyEdit's stdlib-only line debugger. Protocol uses JSON lines on stdin/stdout."""
import bdb
import builtins
import io
import json
import os
import runpy
import sys
import traceback

protocol_in, protocol_out = sys.stdin, sys.stdout

def emit(event, **data):
    protocol_out.write(json.dumps(dict(event=event, **data), ensure_ascii=True) + "\n")
    protocol_out.flush()

def command():
    line = protocol_in.readline()
    if not line:
        raise bdb.BdbQuit
    return json.loads(line)

class Output(io.TextIOBase):
    def __init__(self, event="output"):
        self.event = event
    def write(self, text):
        if text:
            emit(self.event, text=text)
        return len(text)
    def flush(self):
        pass

def read_input(prompt=""):
    emit("input", text=prompt + " [type in RudyEdit console]")
    while True:
        cmd = command()
        if cmd["command"] == "input":
            return cmd.get("text", "")
        if cmd["command"] == "quit":
            raise bdb.BdbQuit

class Debugger(bdb.Bdb):
    def __init__(self, target):
        super().__init__()
        self.target = os.path.abspath(target)
        self.started = False
    def snapshot(self, frame):
        result = {}
        for key, value in list(frame.f_locals.items())[:100]:
            if not key.startswith("__"):
                try:
                    result[key] = repr(value)[:500]
                except Exception:
                    result[key] = "<repr failed>"
        return result
    def configure(self, cmd):
        for path, lines in (cmd.get("breakpoints") or {}).items():
            self.clear_all_file_breaks(path)
            for line in lines:
                self.set_break(path, line)
    def user_line(self, frame):
        if not self.started:
            if os.path.abspath(frame.f_code.co_filename) != self.target:
                return
            self.started = True
        emit("stopped", file=frame.f_code.co_filename, line=frame.f_lineno, locals=self.snapshot(frame))
        while True:
            cmd = command()
            name = cmd["command"]
            if name == "next":
                self.set_next(frame)
                return
            if name == "step":
                self.set_step()
                return
            if name == "out":
                self.set_return(frame)
                return
            if name == "continue":
                self.set_continue()
                return
            if name == "variables":
                emit("variables", text=json.dumps(self.snapshot(frame)))
            if name == "breakpoints":
                self.configure(cmd)
            if name == "quit":
                self.set_quit()
                raise bdb.BdbQuit
    def user_exception(self, frame, exc_info):
        emit("error", text="".join(traceback.format_exception_only(exc_info[0], exc_info[1])))
        self.user_line(frame)

if __name__ == "__main__":
    target = os.path.abspath(sys.argv[1])
    sys.argv = [target]
    sys.path.insert(0, os.path.dirname(target))
    builtins.input = read_input
    sys.stdout = Output()
    sys.stderr = Output("stderr")
    debugger = Debugger(target)
    try:
        debugger.configure(command())
        debugger.runcall(runpy.run_path, target, run_name="__main__")
    except bdb.BdbQuit:
        pass
    except BaseException:
        emit("error", text=traceback.format_exc())
    finally:
        emit("terminated", text="Python debug session ended.")
