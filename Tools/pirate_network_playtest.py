"""Drive opt-in development probes in two separate players; never alter production rooms."""
import json, time, math
from pathlib import Path

root = Path('Temp/NetworkPlaytest')
results = []

def state(peer):
    try: return json.loads((root / peer / 'status.json').read_text(encoding='utf-8-sig'))
    except (OSError, ValueError): return {}

def command(peer, **values):
    folder = root / peer
    folder.mkdir(parents=True, exist_ok=True)
    path = folder / 'command.json'
    deadline = time.monotonic() + 8
    while path.exists() and time.monotonic() < deadline: time.sleep(.1)
    temporary = folder / 'pending.json'
    temporary.write_text(json.dumps(values), encoding='utf-8')
    temporary.replace(path)

def wait(predicate, seconds=15):
    deadline = time.monotonic()+seconds
    while time.monotonic()<deadline:
        if predicate(): return True
        time.sleep(.1)
    return False

def check(label, predicate, seconds=15):
    passed = wait(predicate, seconds)
    results.append(('PASS' if passed else 'FAIL') + ' ' + label)
    (root/'results.txt').write_text('\n'.join(results),encoding='utf-8')
    print(results[-1], flush=True)
    return passed

def distance(a,b): return math.sqrt(sum((a[k]-b[k])**2 for k in ['x','y','z']))

def run():
    if not check('Host and client connect to the same private Photon room', lambda: state('host').get('players')==2 and state('client').get('players')==2, 70): return
    check('Distinct ownership: host slot 0, client slot 1',lambda:state('host').get('localSlot')==0 and state('client').get('localSlot')==1)
    check('Lobby waits for the host',lambda:not state('host').get('started') and not state('client').get('started'))
    command('client',action='start')
    time.sleep(.6)
    check('Client cannot start the authoritative round',lambda:not state('host').get('started'))
    command('host',action='start')
    check('Countdown completes on both peers',lambda:state('host').get('playing') and state('client').get('playing'))
    command('host',action='quiet')
    check('Client menu closes when host starts',lambda:not state('client').get('menu'))
    for i in range(2,6): command('host',action='eliminate',slot=i)
    command('host',action='position',slot=0,x=-3,z=-4,yaw=0)
    command('host',action='position',slot=1,x=0,z=-4,yaw=0)
    time.sleep(.5)
    origin=state('host')['positions'][1]
    command('client',action='controls',x=0,z=1,yaw=0)
    check('Client controls move its authoritative fighter',lambda:distance(state('host')['positions'][1],origin)>.6,4)
    command('client',action='controls',x=0,z=0,yaw=0)
    check('Client sees authoritative movement',lambda:distance(state('client')['positions'][1],state('host')['positions'][1])<.8,5)
    # Reposition for client-authorized grab, lift and release. Targets are selected by the host.
    command('host',action='position',slot=1,x=0,z=-4,yaw=0)
    command('host',action='position',slot=0,x=0,z=-2.8,yaw=0)
    command('client',action='controls',x=0,z=0,yaw=0,grab=True)
    check('Client grabs another player on host',lambda:(state('host')['flags'][1]&8)!=0 and (state('host')['flags'][0]&4)!=0,3)
    command('client',action='controls',x=0,z=0,yaw=0,grab=True,lift=True)
    check('Lift state reaches both peers',lambda:(state('host')['flags'][1]&32)!=0 and (state('client')['flags'][1]&32)!=0,2)
    command('client',action='controls',x=0,z=0,yaw=0,grab=False)
    check('Release removes grab on both peers',lambda:(state('host')['flags'][1]&8)==0 and (state('client')['flags'][1]&8)==0,3)
    command('host',action='eliminate',slot=0)
    check('Same winner and elimination on host and client',lambda:state('host').get('finished') and state('client').get('finished') and state('host').get('winner')==1 and state('client').get('winner')==1)
    command('client',action='start')
    time.sleep(.5)
    check('Client cannot restart a finished round',lambda:state('host').get('round')==1)
    command('host',action='start')
    check('Host restart resets all six fighters on both peers',lambda:state('host').get('round')==2 and state('client').get('round')==2 and all(f&1 for f in state('host')['flags']) and all(f&1 for f in state('client')['flags']))
    check('Second countdown completes',lambda:state('host').get('playing') and state('client').get('playing'))
    command('host',action='quiet')
    for i in range(2,6): command('host',action='eliminate',slot=i)
    command('host',action='position',slot=0,x=-2,z=-7,yaw=0)
    command('host',action='position',slot=1,x=2,z=4,yaw=0)
    command('host',action='cannons')
    check('Cannon warning is replicated',lambda:any(p==1 for p in state('host').get('cannonPhases',[])) and any(p==1 for p in state('client').get('cannonPhases',[])),20)
    check('Cannon projectile is replicated',lambda:any(p==2 for p in state('host').get('cannonPhases',[])) and any(p==2 for p in state('client').get('cannonPhases',[])),15)
    check('Both views agree on shark position',lambda:distance(state('host')['shark'],state('client')['shark'])<2,5)
    command('client',action='leave')
    check('Client disconnect returns control to a bot',lambda:state('host').get('players')==1 and state('host').get('owners',[1,1])[1]==0)
    check('Disconnected client returns to local mode',lambda:not state('client').get('running'))
    command('client',action='join')
    check('Client rejoins the existing room and reclaims a slot',lambda:state('host').get('players')==2 and state('client').get('players')==2 and state('client').get('localSlot')==1,30)
    command('host',action='quit')
    check('Host exit closes the client session and opens the menu',lambda:not state('client').get('running') and state('client').get('menu'),40)
    (root/'done.txt').write_text('\n'.join(results),encoding='utf-8')

if __name__ == '__main__':
    try: run()
    except Exception as error:
        results.append('ERROR '+repr(error)); print(results[-1],flush=True)
        (root/'results.txt').write_text('\n'.join(results),encoding='utf-8')
