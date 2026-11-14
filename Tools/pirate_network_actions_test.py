import time
from pathlib import Path
import pirate_network_playtest as test

test.root = Path('Temp/NetworkActions')
state,command,check,distance = test.state,test.command,test.check,test.distance

def run():
    if not check('Two independent players connect for action checks',lambda:state('host').get('players')==2 and state('client').get('players')==2,50):return
    before_round=state('host')['round']
    command('client',action='controls',x=0,z=0,yaw=0,grab=False)
    command('host',action='start')
    check('Round starts',lambda:state('host').get('round',0)>before_round and state('host').get('playing') and state('client').get('playing'))
    command('host',action='quiet')
    for i in range(2,6):command('host',action='eliminate',slot=i)
    command('host',action='position',slot=0,x=2,z=2,yaw=0)
    command('host',action='position',slot=1,x=2,z=-4,yaw=0)
    time.sleep(.6)
    baseline=state('host')['positions'][1]['y']
    command('client',action='jump')
    check('Client jump produces vertical motion on the host',lambda:state('host')['positions'][1]['y']>baseline+.5,3)
    time.sleep(1.5)
    command('host',action='position',slot=0,x=0,z=-2.5,yaw=0)
    command('host',action='position',slot=1,x=0,z=-4,yaw=0)
    time.sleep(.4)
    before=state('host')['positions'][0]
    command('client',action='push')
    check('Client push displaces another player on the host',lambda:distance(state('host')['positions'][0],before)>.65,3)
    check('Pushed player movement is replicated',lambda:distance(state('host')['positions'][0],state('client')['positions'][0])<.8,3)
    before_round=state('host')['round']
    command('host',action='start')
    check('Fresh round isolates object physics from the push test',lambda:state('host').get('round',0)>before_round and state('host').get('playing') and state('client').get('playing'),8)
    command('host',action='quiet')
    for i in range(2,6):command('host',action='eliminate',slot=i)
    command('host',action='position',slot=0,x=2,z=2,yaw=0)
    prop=state('host')['props'][6]
    command('host',action='position',slot=1,x=prop['x'],z=prop['z']-1.1,yaw=0)
    time.sleep(.5)
    before=state('host')['props'][6]
    command('client',action='controls',x=0,z=0,yaw=0,grab=True,lift=True)
    check('Client can grab a bucket',lambda:state('host')['flags'][1]&8!=0,2)
    check('Lift moves the authoritative bucket upward',lambda:state('host')['props'][6]['y']>before['y']+.55,2)
    before=state('host')['props'][6]
    command('client',action='controls',x=0,z=0,yaw=0,grab=False)
    check('Throw launches the bucket across the deck',lambda:distance(state('host')['props'][6],before)>1,3)
    check('Thrown bucket position is replicated',lambda:distance(state('host')['props'][6],state('client')['props'][6])<1,3)
    command('host',action='position',slot=1,x=0,z=-5,yaw=0)
    command('client',action='controls',x=0,z=0,yaw=0,grab=True)
    time.sleep(.5)
    check('Grab rejects out-of-range targets',lambda:state('host')['flags'][1]&8==0,2)
    command('host',action='eliminate',slot=1)
    check('Eliminated client sees the round result',lambda:state('client').get('finished'))
    actions=state('host')['actions'][1]
    command('client',action='push');command('client',action='jump')
    time.sleep(.8)
    check('Dead client cannot trigger push or jump',lambda:state('host')['actions'][1]==actions)
    (test.root/'done.txt').write_text('\n'.join(test.results),encoding='utf-8')
try:run()
except Exception as error:
    test.results.append('ERROR '+repr(error));print(test.results[-1],flush=True)
    (test.root/'results.txt').write_text('\n'.join(test.results),encoding='utf-8')
