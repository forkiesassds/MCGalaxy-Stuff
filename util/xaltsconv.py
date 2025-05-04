import sys
import os 
import re
from datetime import datetime

c_regex = r"\(((?:[01]\d|2[0-3]):(?:[0-5]\d):(?:[0-5]\d))\) ([a-zA-Z0-9._+]*) \[((?:(?:25[0-5]|(?:2[0-4]|1\d|[1-9]|)\d)\.?\b){4})\] (has joined the server\.|connected)"
d_regex = r"\(((?:[01]\d|2[0-3]):(?:[0-5]\d):(?:[0-5]\d))\) ([a-zA-Z0-9._+]*) \(((?:(?:25[0-5]|(?:2[0-4]|1\d|[1-9]|)\d)\.?\b){4})\) disconnected."

class XAltsRecord:
    username: str
    ip: str
    first_seen: datetime
    last_seen: datetime
    
    def __init__(self, username, ip, date):
        self.username = username
        self.ip = ip
        self.first_seen = date
        self.last_seen = date

args = sys.argv[1:]

if len(args) == 0:
    print('usage: xaltsconv.py [log dir] <cc_plus>')
    exit()

directory = args[0]
cc_plus = False

if len(args) > 1:
    try:
        cc_plus = bool(args[1])
    except:
        print(f"{args[1]} is not true or false")
        exit()

records: list[XAltsRecord] = []

def parse_regex(line, regex):
    matches = re.finditer(regex, line)
    matches = tuple(matches)
    
    if len(matches) == 1:
        time = matches[0].group(1)
        username = matches[0].group(2) + ('+' if cc_plus else '')
        ip = matches[0].group(3)
        
        hour: datetime
        
        try:
            hour = datetime.strptime(time, '%H:%M:%S')
        except:
            return
            
        record_time = day + (hour - datetime(1900, 1, 1))
        
        existing_record = [(idx, r) for idx, r in enumerate(records) if r.username == username and r.ip == ip]
        if len(existing_record) == 0:
            print(f"adding new record for player {username} on ip {ip}")
            record = XAltsRecord(username, ip, record_time)
            records.append(record)
        else:
            print(f"updating record for player {username} on ip {ip}")
            record = existing_record[0][1]
            if record.last_seen < record_time:
                record.last_seen = record_time
                
            if record.first_seen > record_time:
                record.first_seen = record_time
            records[existing_record[0][0]] = record

for path in os.scandir(directory):
    if path.is_file():
        day: datetime
        
        try:
            day = datetime.strptime(os.path.splitext(path.name)[0], '%Y-%m-%d')
        except:
            continue
            
        with open(path.path, 'r') as file:
            for line in file:
                if 'Unsupported protocol version' in line or \
                    'Usernames must be between 1 and 16 characters' in line or \
                    'Invalid player name' in line or \
                    'Login failed! Close the game and sign in again.' in line:
                    continue

                parse_regex(line, c_regex)
                parse_regex(line, d_regex)
            
records.sort(key=lambda r: r.first_seen)
print('generating SQL file')
with open('xalts.sql', 'w') as out_file:
    out_file.write("""DROP TABLE IF EXISTS `LinkedIPs`;
CREATE TABLE `LinkedIPs` (
Name CHAR(20),
IP CHAR(15),
FirstSeen DATETIME,
LastSeen DATETIME
);
""")

    for record in records:
        out_file.write(f"INSERT INTO `LinkedIPs` VALUES('{record.username}','{record.ip}','{record.first_seen.strftime('%Y-%m-%d %H:%M:%S')}','{record.last_seen.strftime('%Y-%m-%d %H:%M:%S')}');\n")

print('wrote SQL file to xalts.sql')
