import { useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { Braces, Maximize2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import CopyableId from '../../components/CopyableId.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import JsonViewerModal from '../../components/JsonViewerModal.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import Modal from '../../components/Modal.jsx';
import FindingDetail, { useFindingDetail } from './FindingDetail.jsx';

// A finding in a side drawer, with links to its full page and its JSON.
export default function FindingDetailDrawer({ findingId, onClose }) {
  const { t } = useTranslation();
  const { detail, error, reload, clearError } = useFindingDetail(findingId);
  const [json, setJson] = useState(false);
  return (
    <Modal
      open={Boolean(findingId)}
      drawer
      title={t('findings.detail.title')}
      subtitle={findingId ? <CopyableId value={findingId} /> : null}
      onClose={onClose}
      headerExtra={
        findingId ? (
          <div className="row-actions">
            <Link className="icon-button" to={'/dashboard/findings/' + encodeURIComponent(findingId)} aria-label={t('findings.openPage')} title={t('findings.openPage')}>
              <Maximize2 size={16} aria-hidden="true" />
            </Link>
            <button type="button" className="icon-button" onClick={() => setJson(true)} disabled={!detail} aria-label={t('common.viewJson')} title={t('common.viewJson')}>
              <Braces size={16} aria-hidden="true" />
            </button>
          </div>
        ) : null
      }
    >
      <ErrorState error={error} inline onRetry={reload} onDismiss={clearError} />
      {!detail && !error ? <LoadingState /> : null}
      {detail ? <FindingDetail detail={detail} /> : null}
      <JsonViewerModal open={json && Boolean(detail)} value={detail} onClose={() => setJson(false)} />
    </Modal>
  );
}

FindingDetailDrawer.propTypes = {
  findingId: PropTypes.string,
  onClose: PropTypes.func.isRequired
};
