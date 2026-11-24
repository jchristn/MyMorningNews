"""Platform wire contract regression tests. (c) 2026 Lattice AI LLC."""
import json
import unittest
from urllib.parse import urlparse, parse_qs
import requests
from mymorningnews_sdk import MyMorningNewsClient, BillingOfferResponse, EffectivePlan, ProviderConnection


class Transport(requests.Session):
    def __init__(self):
        super().__init__()
        self.sent = []
        self.reply = {}

    def send(self, request, **kwargs):
        self.sent.append(request)
        result = requests.Response()
        result.status_code = 200
        result.headers['Content-Type'] = 'application/json'
        result._content = json.dumps(self.reply).encode()
        result.request = request
        return result


class PlatformTest(unittest.TestCase):
    def setUp(self):
        self.transport = Transport()
        self.api = MyMorningNewsClient('https://example.invalid', token='member', session=self.transport)

    def test_nested_billing_and_provider_shapes(self):
        self.transport.reply = {'enabled': False, 'offers': [{'key': 'paid-5-year', 'usdCents': 10000}], 'plan': {'subscriptionRequired': False, 'features': {'customTopics.max': '0'}}, 'subscriptions': [{'endsUtc': None}]}
        status = self.api.billing.status()
        self.assertIsInstance(status.offers[0], BillingOfferResponse)
        self.assertIsInstance(status.plan, EffectivePlan)
        self.assertFalse(status.plan.subscription_required)
        self.assertEqual(status.offers[0].usd_cents, 10000)
        self.assertIsNone(status.subscriptions[0].ends_utc)
        self.transport.reply = {'connections': [{'id': 'pc1', 'secretLast4': 'abcd'}], 'localSpeechAvailable': False}
        connections = self.api.providers.list()
        self.assertIsInstance(connections.connections[0], ProviderConnection)
        self.assertEqual(connections.connections[0].secret_last4, 'abcd')
        self.assertFalse(hasattr(connections.connections[0], 'secret_encrypted'))

    def test_scoped_catalog_paging_progress_and_ranges(self):
        self.transport.reply = {'items': [{'id': 'cat1', 'eligible': True}], 'pageNumber': 2, 'totalCount': 1}
        page = self.api.catalog.list(page_number=2, category='AI & science')
        self.assertTrue(page.items[0].eligible)
        request = self.transport.sent[-1]
        self.assertEqual(parse_qs(urlparse(request.url).query)['pageNumber'], ['2'])
        self.assertEqual(request.headers['Authorization'], 'Bearer member')
        self.transport.reply = {'positionSeconds': 12.5, 'completed': False}
        self.api.catalog.save_progress('cat/one', 'issue?one', {'position_seconds': 12.5, 'completed': False})
        request = self.transport.sent[-1]
        self.assertEqual(urlparse(request.url).path, '/v1.0/api/catalog/cat%2Fone/issues/issue%3Fone/progress')
        self.assertEqual(json.loads(request.body), {'positionSeconds': 12.5, 'completed': False})
        self.api.catalog.audio('cat1', 'b1', byte_range=(10, 20))
        self.assertEqual(self.transport.sent[-1].headers['Range'], 'bytes=10-20')

    def test_write_contracts_preserve_zero_and_standalone_history(self):
        self.api.billing.checkout({'offer_key': 'paid-5-year'})
        self.assertEqual(json.loads(self.transport.sent[-1].body), {'offerKey': 'paid-5-year'})
        self.api.providers.set_funding({'mode': 'Personal'})
        self.assertTrue(self.transport.sent[-1].url.endswith('/account/funding'))
        self.api.admin_entitlements.set('t1', 'u1', {'feature_key': 'audio.dailySeconds.max', 'value': '0', 'reason': 'Account restriction'})
        self.assertEqual(json.loads(self.transport.sent[-1].body)['value'], '0')
        self.api.admin_catalog.save_research('cat1', {'topic': {'history_mode': 'Off', 'audio_enabled': False}, 'schedule': None})
        self.assertEqual(json.loads(self.transport.sent[-1].body), {'topic': {'historyMode': 'Off', 'audioEnabled': False}, 'schedule': None})


if __name__ == '__main__':
    unittest.main()
