"""Combine completed target reports without turning blocked tests into passes."""
import argparse
import json
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--output',required=True,type=Path);p.add_argument('reports',nargs='+',type=Path);a=p.parse_args()
reports=[json.loads(path.read_text(encoding='utf-8')) for path in a.reports]
rows=[dict(row,target=report['rid']) for report in reports for row in report['results']]
stats={key:sum(r['summary'][key] for r in reports) for key in ('performed','passed','errors','blocked')}
lines=[f'Tests performed: {stats["performed"]}',f'Passed: {stats["passed"]}',f'Errors: {stats["errors"]}',f'Blocked: {stats["blocked"]}','Result: '+('PASS' if all(r['passed'] for r in reports) else 'NOT ALL TARGETS RELEASE-READY'),'','TARGET RESULTS']
for report in reports:lines.append(f'{report["rid"]}: {"PASS" if report["passed"] else "BLOCKED / FAIL"}; '+json.dumps(report['summary'])+'; completed UTC '+report['completed_utc'])
lines.extend(['','ERRORS FOUND'])
errors=[r for r in rows if r['status']!='PASS']
if not errors:lines.append('None.')
for row in errors:lines.extend([f'[{row["status"]}] {row["target"]} / {row["name"]}',row.get('error',''),''])
lines.extend(['','ALL TESTS PERFORMED (named cases/checkpoints; may contain multiple assertions)'])
for i,row in enumerate(rows,1):lines.append(f'{i:04d}. [{row["status"]}] {row["target"]} / {row.get("layer","release")} / {row["name"]}')
lines.extend(['','A blocked Windows native run is not a product-test pass. No Windows installer may be created until its native report passes.','External protocol fixtures do not certify all optional third-party toolchains or GUI terminal/font implementations.'])
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text('\n'.join(lines)+'\n',encoding='utf-8')
a.output.with_suffix('.json').write_text(json.dumps(dict(schema=1,summary=stats,targets=reports),indent=2,ensure_ascii=False),encoding='utf-8')
print('\n'.join(lines[:5]))
