"""DAP framing fixture; does not launch a debugger or execute a target."""
import json,sys
sequence=0

def emit(message):
 global sequence
 sequence+=1;message['seq']=sequence
 data=json.dumps(message).encode();sys.stdout.buffer.write(f'Content-Length: {len(data)}\r\n\r\n'.encode()+data);sys.stdout.buffer.flush()
while True:
 headers={}
 while True:
  line=sys.stdin.buffer.readline()
  if not line:sys.exit(0)
  if line==b'\r\n':break
  key,value=line.decode().split(':',1);headers[key.lower()]=value.strip()
 request=json.loads(sys.stdin.buffer.read(int(headers['content-length'])))
 command=request['command']
 body={'stackFrames':[{'id':1,'name':'main','line':7,'source':{'path':'Program.cs'}}]} if command=='stackTrace' else {}
 emit({'type':'response','request_seq':request['seq'],'command':command,'success':True,'body':body})
 if command=='configurationDone':emit({'type':'event','event':'stopped','body':{'threadId':1,'reason':'entry'}})
