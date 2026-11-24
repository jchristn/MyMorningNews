#!/usr/bin/env python3
"""Create unsubmitted Apple subscriptions and draft Google base plans, initially priced in the US.
Does not submit app builds, activate Google purchases, or change existing prices.
Requires requests and cryptography. (c) 2026 Lattice AI LLC
"""
import argparse
from decimal import Decimal
import json
import os
from pathlib import Path
from store_auth import apple_session, google_session


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--provider', choices=['apple', 'google'], required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--app-id', default='6837840760')
    parser.add_argument('--bundle', default='ai.latticetech.mymorningnews')
    parser.add_argument('--issuer')
    parser.add_argument('--key-id')
    parser.add_argument('--key-file', required=True)
    args = parser.parse_args()
    output = Path(args.output).expanduser().resolve()
    if output.is_relative_to(Path(__file__).resolve().parents[2]):
        raise SystemExit('Store configuration output must be outside the repository.')
    result = json.loads(output.read_text()) if output.exists() else {'offers': {}}

    def save():
        output.parent.mkdir(parents=True, exist_ok=True)
        descriptor = os.open(output, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
        os.chmod(output, 0o600)
        with os.fdopen(descriptor, 'w') as file:
            json.dump(result, file, indent=2)
            file.write('\n')

    session = apple_session(args.issuer, args.key_id, args.key_file) if args.provider == 'apple' else google_session(args.key_file)
    base = 'https://api.appstoreconnect.apple.com' if args.provider == 'apple' else 'https://androidpublisher.googleapis.com/androidpublisher/v3/applications/' + args.bundle

    def call(method, path, body=None, params=None):
        response = session.request(method, path if path.startswith('https://') else base + path, json=body, params=params, timeout=40)
        if not response.ok:
            data = response.json()
            errors = data.get('errors', [data.get('error', {})])
            messages = [e.get('detail', e.get('message', '')) for e in errors if isinstance(e, dict)]
            raise RuntimeError(f'{args.provider} {method} {path}: HTTP {response.status_code}: ' + '; '.join(messages))
        return response.json() if response.content else {}

    def rows(path, params=None):
        values = []
        while path:
            response = call('GET', path, params=params)
            values.extend(response.get('data', []))
            path = response.get('links', {}).get('next')
            params = None
        return values

    def payload(kind, attributes, relationship, parent_type, parent_id):
        return {'data': {'type': kind, 'attributes': attributes, 'relationships': {relationship: {'data': {'type': parent_type, 'id': parent_id}}}}}

    if args.provider == 'apple':
        app = call('GET', '/v1/apps/' + args.app_id)['data']
        if app['attributes']['bundleId'] != args.bundle:
            raise RuntimeError('App identity differs from the configured bundle.')
        groups = rows('/v1/apps/' + args.app_id + '/subscriptionGroups')
        group = next((g for g in groups if g['attributes']['referenceName'] == 'My Morning News'), None)
        if group is None:
            group = call('POST', '/v1/subscriptionGroups', payload('subscriptionGroups', {'referenceName': 'My Morning News'}, 'app', 'apps', args.app_id))['data']
        result['groupId'] = group['id']
        save()
        localizations = rows('/v1/subscriptionGroups/' + group['id'] + '/subscriptionGroupLocalizations')
        if not any(l['attributes']['locale'] == 'en-US' for l in localizations):
            call('POST', '/v1/subscriptionGroupLocalizations', payload('subscriptionGroupLocalizations', {'name': 'My Morning News', 'locale': 'en-US'}, 'subscriptionGroup', 'subscriptionGroups', group['id']))
        subscriptions = rows('/v1/subscriptionGroups/' + group['id'] + '/subscriptions')
        for count, monthly, yearly, minutes in [(5, 10, 100, 30), (20, 30, 300, 100)]:
            for interval, amount, period in [('month', monthly, 'ONE_MONTH'), ('year', yearly, 'ONE_YEAR')]:
                offer = f'paid-{count}-{interval}'
                product = args.bundle + f'.paid{count}.{interval}'
                subscription = next((s for s in subscriptions if s['attributes']['productId'] == product), None)
                if subscription is None:
                    subscription = call('POST', '/v1/subscriptions', payload('subscriptions', {
                        'name': f'My Morning News {count} {interval.title()}', 'productId': product,
                        'subscriptionPeriod': period, 'groupLevel': 1 if count == 20 else 2, 'familySharable': False,
                        'reviewNote': f'{count} custom briefing topics plus system catalog subscriptions; {minutes} generated audio minutes per UTC day. No free trial. Sign in, open Account, then Plans and billing.'}, 'group', 'subscriptionGroups', group['id']))['data']
                result['offers'][offer] = {'productId': product, 'subscriptionId': subscription['id'], 'usdCents': amount * 100, 'state': subscription['attributes'].get('state')}
                save()
                sid = subscription['id']
                if subscription['attributes'].get('multiSeatStatus') != 'DISABLED' or subscription['attributes'].get('marketSettings') != ['APP_STORE']:
                    call('PATCH', '/v1/subscriptions/' + sid, {'data': {'type': 'subscriptions', 'id': sid, 'attributes': {'multiSeatStatus': 'DISABLED', 'marketSettings': ['APP_STORE']}}})
                availability = rows('/v1/subscriptions/' + sid + '/planAvailabilities')
                if not any(a['attributes']['planType'] == 'UPFRONT' for a in availability):
                    body = payload('subscriptionPlanAvailabilities', {'planType': 'UPFRONT', 'availableInNewTerritories': False}, 'subscription', 'subscriptions', sid)
                    body['data']['relationships']['availableTerritories'] = {'data': [{'type': 'territories', 'id': 'USA'}]}
                    call('POST', '/v1/subscriptionPlanAvailabilities', body)
                localizations = rows('/v1/subscriptions/' + sid + '/subscriptionLocalizations')
                if not any(l['attributes']['locale'] == 'en-US' for l in localizations):
                    call('POST', '/v1/subscriptionLocalizations', payload('subscriptionLocalizations', {'name': f'Morning News {count} {interval.title()}', 'locale': 'en-US', 'description': f'{count} custom briefings. {minutes} audio minutes per day.'}, 'subscription', 'subscriptions', sid))
                current = rows('/v1/subscriptions/' + sid + '/prices', {'filter[territory]': 'USA', 'include': 'subscriptionPricePoint'})
                if not current:
                    points = rows('/v1/subscriptions/' + sid + '/pricePoints', {'filter[territory]': 'USA', 'limit': 200})
                    point = next((p for p in points if Decimal(p['attributes']['customerPrice']) == Decimal(amount)), None)
                    if point is None:
                        raise RuntimeError(f'Apple has no exact USD {amount} price point; no replacement price was selected.')
                    body = payload('subscriptionPrices', {'preserveCurrentPrice': True, 'planType': 'UPFRONT'}, 'subscription', 'subscriptions', sid)
                    body['data']['relationships']['subscriptionPricePoint'] = {'data': {'type': 'subscriptionPricePoints', 'id': point['id']}}
                    call('POST', '/v1/subscriptionPrices', body)
        print('Apple: four subscriptions prepared with US pricing; review submission and availability are separate release steps.')
    else:
        existing = call('GET', '/subscriptions').get('subscriptions', [])
        for count, monthly, yearly, minutes in [(5, 10, 100, 30), (20, 30, 300, 100)]:
            product = f'mmn_paid_{count}'
            subscription = next((s for s in existing if s['productId'] == product), None)
            if subscription is None:
                converted = call('POST', '/pricing:convertRegionPrices', {'price': {'currencyCode': 'USD', 'units': str(monthly)}})
                body = {'packageName': args.bundle, 'productId': product, 'listings': [{'languageCode': 'en-US', 'title': f'My Morning News {count}',
                    'description': f'{count} custom briefing topics, system catalog subscriptions, and {minutes} generated audio minutes per UTC day. No trial.',
                    'benefits': [f'{count} custom briefing topics', 'System catalog subscriptions', f'{minutes} generated audio minutes per day']}], 'basePlans': []}
                for interval, amount, duration in [('month', monthly, 'P1M'), ('year', yearly, 'P1Y')]:
                    body['basePlans'].append({'basePlanId': interval, 'autoRenewingBasePlanType': {'billingPeriodDuration': duration, 'gracePeriodDuration': 'P3D', 'resubscribeState': 'RESUBSCRIBE_STATE_ACTIVE'},
                                             'regionalConfigs': [{'regionCode': 'US', 'newSubscriberAvailability': True, 'price': {'currencyCode': 'USD', 'units': str(amount)}}]})
                subscription = call('POST', '/subscriptions', body, {'productId': product, 'regionsVersion.version': converted['regionVersion']['version']})
            for interval, amount in [('month', monthly), ('year', yearly)]:
                plan = next(p for p in subscription['basePlans'] if p['basePlanId'] == interval)
                result['offers'][f'paid-{count}-{interval}'] = {'productId': product, 'basePlanId': interval, 'usdCents': amount * 100, 'state': plan['state']}
            save()
        print('Google: two subscriptions and four draft base plans prepared with US pricing. Activation is a separate release step.')


if __name__ == '__main__':
    main()
