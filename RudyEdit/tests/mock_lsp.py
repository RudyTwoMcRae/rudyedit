"""Small protocol fixture: real stdio framing, diagnostics and ranged UTF-16 edits."""
import json, sys

def emit(value):
 data=json.dumps(value).encode();sys.stdout.buffer.write(f'Content-Length: {len(data)}\r\n\r\n'.encode()+data);sys.stdout.buffer.flush()
while True:
 headers={}
 while True:
  line=sys.stdin.buffer.readline()
  if not line:sys.exit(0)
  if line==b'\r\n':break
  name,value=line.decode().split(':',1);headers[name.lower()]=value.strip()
 message=json.loads(sys.stdin.buffer.read(int(headers['content-length'])))
 method=message.get('method')
 if method=='initialize':result={'capabilities':{'textDocumentSync':1,'completionProvider':{}}}
 elif method=='textDocument/completion':result=[{'label':'print','textEdit':{'range':{'start':{'line':0,'character':3},'end':{'line':0,'character':6}},'newText':'print'}}]
 else:result=None
 if method=='textDocument/didOpen':
  uri=message['params']['textDocument']['uri']
  emit({'jsonrpc':'2.0','method':'textDocument/publishDiagnostics','params':{'uri':uri,'diagnostics':[{'range':{'start':{'line':0,'character':3},'end':{'line':0,'character':6}},'message':'fixture diagnostic'}]}})
 if 'id' in message:emit({'jsonrpc':'2.0','id':message['id'],'result':result})
